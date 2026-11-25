using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Decorlim.Desktop.Models;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Decorlim.Desktop.Services
{
    public class ApiService
    {
        private readonly HttpClient _http;
        private readonly ResiliencePipeline<HttpResponseMessage> _politicaHttp;
        private readonly string _carpetaDatos;
        private readonly string _rutaProductosCache;
        private readonly string _rutaClientesCache;
        private readonly string _rutaUsuariosCache;
        private readonly string _rutaVentasPendientes;
        private readonly string _rutaVentasLocales;

        public bool ApiEnLinea { get; private set; }
        public string BaseUrl { get; }

        public ApiService(string baseUrl = "http://localhost:5041/api/")
        {
            BaseUrl = baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/";
            _http = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                // Timeout superior al de Polly para que Polly controle el corte limpiamente
                Timeout = TimeSpan.FromSeconds(10)
            };

            // Política Polly: 1 reintento rápido (300ms) ante errores de red/5xx + Timeout estricto de 2.5s por intento
            _politicaHttp = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.FromMilliseconds(300),
                    BackoffType = DelayBackoffType.Constant,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .HandleResult(r => (int)r.StatusCode >= 500)
                })
                .AddTimeout(new TimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromMilliseconds(2500)
                })
                .Build();

            _carpetaDatos = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DECORLIM_POS");
            Directory.CreateDirectory(_carpetaDatos);

            _rutaProductosCache = Path.Combine(_carpetaDatos, "productos_cache.json");
            _rutaClientesCache = Path.Combine(_carpetaDatos, "clientes_cache.json");
            _rutaUsuariosCache = Path.Combine(_carpetaDatos, "usuarios_cache.json");
            _rutaVentasPendientes = Path.Combine(_carpetaDatos, "ventas_pendientes.json");
            _rutaVentasLocales = Path.Combine(_carpetaDatos, "ventas_locales.json");
        }

        private Task<HttpResponseMessage> EjecutarHttpAsync(Func<Task<HttpResponseMessage>> accionHttp)
        {
            return _politicaHttp.ExecuteAsync(async _ => await accionHttp()).AsTask();
        }

        // ==================== AUTENTICACIÓN (ONLINE + RESPALDO OFFLINE) ====================
        public async Task<(bool Ok, UsuarioSesion? Sesion, string Mensaje)> LoginAsync(string login, string password)
        {
            string loginLimpio = (login ?? string.Empty).Trim();

            try
            {
                var payload = JsonConvert.SerializeObject(new LoginRequest { Login = loginLimpio, Password = password });
                var resp = await EjecutarHttpAsync(() =>
                {
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    return _http.PostAsync("auth/login", content);
                });

                if (resp.IsSuccessStatusCode)
                {
                    ApiEnLinea = true;
                    string json = await resp.Content.ReadAsStringAsync();
                    var sesion = JsonConvert.DeserializeObject<UsuarioSesion>(json);
                    if (sesion != null)
                    {
                        GuardarUsuarioEnCacheLocal(sesion);
                        return (true, sesion, "Sesión iniciada en línea.");
                    }
                }

                ApiEnLinea = true;
                string err = await resp.Content.ReadAsStringAsync();
                return (false, null, string.IsNullOrWhiteSpace(err) ? "Credenciales inválidas." : err.Trim('"'));
            }
            catch
            {
                ApiEnLinea = false;
                var cache = LeerJson<List<UsuarioSesion>>(_rutaUsuariosCache) ?? new List<UsuarioSesion>();
                var defectuosos = CrearUsuariosOfflinePorDefecto();
                foreach (var u in defectuosos)
                {
                    if (!cache.Any(x => string.Equals(x.Login, u.Login, StringComparison.OrdinalIgnoreCase)))
                        cache.Add(u);
                }

                var encontrado = cache.FirstOrDefault(u =>
                    string.Equals(u.Login, loginLimpio, StringComparison.OrdinalIgnoreCase));

                if (encontrado != null)
                {
                    return (true, encontrado, "Sesión iniciada en Modo Offline.");
                }

                return (false, null, "Sin conexión con la API y el usuario no está en caché local.");
            }
        }

        private void GuardarUsuarioEnCacheLocal(UsuarioSesion sesion)
        {
            var lista = LeerJson<List<UsuarioSesion>>(_rutaUsuariosCache) ?? new List<UsuarioSesion>();
            lista.RemoveAll(u => string.Equals(u.Login, sesion.Login, StringComparison.OrdinalIgnoreCase));
            lista.Add(sesion);
            EscribirJson(_rutaUsuariosCache, lista);
        }

        private static List<UsuarioSesion> CrearUsuariosOfflinePorDefecto() => new()
        {
            new UsuarioSesion { IdUsuario = 1, Login = "admin", NombreCompleto = "Administrador DECORLIM", Rol = "Administrador" },
            new UsuarioSesion { IdUsuario = 2, Login = "mtorres_admin", NombreCompleto = "Mariana Torres", Rol = "Administrador" },
            new UsuarioSesion { IdUsuario = 3, Login = "cflores_vendedor", NombreCompleto = "Cristian Flores (Vendedor)", Rol = "Vendedor" }
        };

        // ==================== PRODUCTOS E INVENTARIO ====================
        public async Task<List<ProductoItem>> ObtenerProductosAsync(bool incluirInactivos = false)
        {
            try
            {
                var resp = await EjecutarHttpAsync(() =>
                    _http.GetAsync($"productos?incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}"));
                resp.EnsureSuccessStatusCode();
                ApiEnLinea = true;

                string json = await resp.Content.ReadAsStringAsync();
                var lista = JsonConvert.DeserializeObject<List<ProductoItem>>(json) ?? new List<ProductoItem>();
                EscribirJson(_rutaProductosCache, lista);
                return lista;
            }
            catch
            {
                ApiEnLinea = false;
                var cache = LeerJson<List<ProductoItem>>(_rutaProductosCache) ?? new List<ProductoItem>();
                return incluirInactivos ? cache : cache.Where(p => p.Activo).ToList();
            }
        }

        public async Task<(bool Ok, ProductoItem? Producto, string Mensaje)> GuardarProductoAsync(ProductoItem producto, bool esNuevo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(producto.Presentacion))
                    producto.Presentacion = "Unidad";

                string json = JsonConvert.SerializeObject(producto);
                var resp = await EjecutarHttpAsync(() =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    return esNuevo
                        ? _http.PostAsync("productos", content)
                        : _http.PutAsync($"productos/{producto.IdProducto}", content);
                });

                string body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    ApiEnLinea = true;
                    return (false, null, string.IsNullOrWhiteSpace(body) ? $"Error HTTP {(int)resp.StatusCode}" : body.Trim('"'));
                }

                ApiEnLinea = true;
                var guardado = JsonConvert.DeserializeObject<ProductoItem>(body);
                if (guardado != null)
                {
                    var cache = LeerJson<List<ProductoItem>>(_rutaProductosCache) ?? new List<ProductoItem>();
                    if (esNuevo)
                    {
                        cache.Add(guardado);
                    }
                    else
                    {
                        var idx = cache.FindIndex(p => p.IdProducto == guardado.IdProducto);
                        if (idx >= 0) cache[idx] = guardado;
                        else cache.Add(guardado);
                    }
                    EscribirJson(_rutaProductosCache, cache);
                }

                return (true, guardado, esNuevo ? "Producto creado correctamente." : "Inventario actualizado.");
            }
            catch
            {
                ApiEnLinea = false;
                var cache = LeerJson<List<ProductoItem>>(_rutaProductosCache) ?? new List<ProductoItem>();
                if (esNuevo)
                {
                    producto.IdProducto = cache.Count > 0 ? cache.Max(p => p.IdProducto) + 1 : 1;
                    if (string.IsNullOrWhiteSpace(producto.CodigoBarras))
                        producto.CodigoBarras = $"DEC-{producto.IdProducto:D5}";
                    cache.Add(producto);
                }
                else
                {
                    var idx = cache.FindIndex(p => p.IdProducto == producto.IdProducto);
                    if (idx >= 0) cache[idx] = producto;
                }
                EscribirJson(_rutaProductosCache, cache);
                return (true, producto, "Guardado en caché local (Modo Offline).");
            }
        }

        // ==================== CLIENTES ====================
        public async Task<List<ClienteItem>> ObtenerClientesAsync()
        {
            try
            {
                var resp = await EjecutarHttpAsync(() => _http.GetAsync("clientes"));
                resp.EnsureSuccessStatusCode();
                ApiEnLinea = true;

                string json = await resp.Content.ReadAsStringAsync();
                var lista = JsonConvert.DeserializeObject<List<ClienteItem>>(json) ?? new List<ClienteItem>();
                AsegurarConsumidorFinal(lista);
                EscribirJson(_rutaClientesCache, lista);
                return lista;
            }
            catch
            {
                ApiEnLinea = false;
                var cache = LeerJson<List<ClienteItem>>(_rutaClientesCache) ?? new List<ClienteItem>();
                AsegurarConsumidorFinal(cache);
                return cache;
            }
        }

        private static void AsegurarConsumidorFinal(List<ClienteItem> lista)
        {
            if (!lista.Any(c => c.Identificacion == "9999999999999"))
            {
                lista.Insert(0, new ClienteItem
                {
                    IdCliente = 1,
                    TipoIdentificacion = "CONSUMIDOR_FINAL",
                    Identificacion = "9999999999999",
                    RazonSocial = "CONSUMIDOR FINAL",
                    Direccion = "S/N",
                    Telefono = "9999999999",
                    Celular = "9999999999",
                    Correo = "consumidor@decorlim.local",
                    TipoCliente = "General",
                    Activo = true
                });
            }
        }

        public async Task<(bool Ok, ClienteItem? Cliente, string Mensaje)> GuardarClienteAsync(ClienteItem cliente, bool esNuevo)
        {
            try
            {
                string json = JsonConvert.SerializeObject(cliente);
                var resp = await EjecutarHttpAsync(() =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    return esNuevo
                        ? _http.PostAsync("clientes", content)
                        : _http.PutAsync($"clientes/{cliente.IdCliente}", content);
                });

                string body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    return (false, null, body.Trim('"'));
                }

                ApiEnLinea = true;
                var guardado = JsonConvert.DeserializeObject<ClienteItem>(body);
                if (guardado != null)
                {
                    var cache = LeerJson<List<ClienteItem>>(_rutaClientesCache) ?? new List<ClienteItem>();
                    if (esNuevo)
                    {
                        cache.Add(guardado);
                    }
                    else
                    {
                        var idx = cache.FindIndex(c => c.IdCliente == guardado.IdCliente);
                        if (idx >= 0) cache[idx] = guardado;
                        else cache.Add(guardado);
                    }
                    EscribirJson(_rutaClientesCache, cache);
                }

                return (true, guardado, esNuevo ? "Cliente registrado correctamente." : "Cliente actualizado.");
            }
            catch
            {
                ApiEnLinea = false;
                var cache = LeerJson<List<ClienteItem>>(_rutaClientesCache) ?? new List<ClienteItem>();
                if (esNuevo)
                {
                    cliente.IdCliente = cache.Count > 0 ? cache.Max(c => c.IdCliente) + 1 : 2;
                    cache.Add(cliente);
                }
                else
                {
                    var idx = cache.FindIndex(c => c.IdCliente == cliente.IdCliente);
                    if (idx >= 0) cache[idx] = cliente;
                }
                EscribirJson(_rutaClientesCache, cache);
                return (true, cliente, "Cliente guardado localmente (Modo Offline).");
            }
        }

        // ==================== USUARIOS (SOLO ADMINISTRADOR) ====================
        public async Task<List<UsuarioItem>> ObtenerUsuariosAsync()
        {
            try
            {
                var resp = await EjecutarHttpAsync(() => _http.GetAsync("usuarios"));
                resp.EnsureSuccessStatusCode();
                ApiEnLinea = true;
                string json = await resp.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<UsuarioItem>>(json) ?? new List<UsuarioItem>();
            }
            catch
            {
                ApiEnLinea = false;
                throw;
            }
        }

        public async Task<(bool Ok, string Mensaje)> CrearUsuarioAsync(CrearUsuarioRequest req)
        {
            try
            {
                string payload = JsonConvert.SerializeObject(req);
                var resp = await EjecutarHttpAsync(() =>
                {
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    return _http.PostAsync("usuarios", content);
                });
                string body = await resp.Content.ReadAsStringAsync();
                ApiEnLinea = true;
                return resp.IsSuccessStatusCode
                    ? (true, "Usuario creado correctamente.")
                    : (false, string.IsNullOrWhiteSpace(body) ? "No se pudo crear el usuario." : body.Trim('"'));
            }
            catch (Exception ex)
            {
                ApiEnLinea = false;
                return (false, $"Sin conexión con el servidor: {ex.Message}");
            }
        }

        public async Task<(bool Ok, string Mensaje)> ActualizarUsuarioAsync(int id, ActualizarUsuarioRequest req)
        {
            try
            {
                string payload = JsonConvert.SerializeObject(req);
                var resp = await EjecutarHttpAsync(() =>
                {
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    return _http.PutAsync($"usuarios/{id}", content);
                });
                string body = await resp.Content.ReadAsStringAsync();
                ApiEnLinea = true;
                return resp.IsSuccessStatusCode
                    ? (true, "Usuario actualizado correctamente.")
                    : (false, string.IsNullOrWhiteSpace(body) ? "No se pudo actualizar el usuario." : body.Trim('"'));
            }
            catch (Exception ex)
            {
                ApiEnLinea = false;
                return (false, $"Sin conexión con el servidor: {ex.Message}");
            }
        }

        // ==================== VENTAS, COMPROBANTES Y COLA OFFLINE ====================
        public int ContarVentasPendientes()
        {
            var pendientes = LeerJson<List<RegistrarVentaRequest>>(_rutaVentasPendientes);
            return pendientes?.Count ?? 0;
        }

        public List<VentaHistorialItem> ObtenerVentasLocales()
            => LeerJson<List<VentaHistorialItem>>(_rutaVentasLocales) ?? new List<VentaHistorialItem>();

        public void GuardarVentaLocalHistorial(RegistrarVentaResponse resp, string usuarioLogin, string tarifa, bool esOffline)
        {
            var lista = ObtenerVentasLocales();
            lista.Insert(0, new VentaHistorialItem
            {
                IdVenta = resp.IdVenta,
                FechaHora = resp.FechaHora,
                Vendedor = usuarioLogin,
                Cliente = resp.ClienteNombre,
                Identificacion = resp.ClienteIdentificacion,
                DireccionCliente = resp.ClienteDireccion,
                TelefonoCliente = resp.ClienteTelefono,
                CelularCliente = resp.ClienteCelular,
                CorreoCliente = resp.ClienteCorreo,
                Tarifa = tarifa,
                Subtotal = resp.Subtotal,
                Iva = resp.Iva,
                Total = resp.Total,
                MetodoPago = resp.MetodoPago,
                ReferenciaPago = resp.ReferenciaPago,
                Secuencial = resp.Secuencial,
                EstadoSri = esOffline ? "OfflinePendiente" : (resp.TipoDocumento == "NotaVenta" ? "NotaVenta" : "Pendiente")
            });
            EscribirJson(_rutaVentasLocales, lista.Take(1000).ToList());
        }

        public async Task<(bool Ok, bool GuardadoOffline, RegistrarVentaResponse? Comprobante, string Mensaje)> RegistrarVentaAsync(
            RegistrarVentaRequest request,
            ClienteItem clienteSeleccionado,
            List<CarritoItem> itemsCarrito)
        {
            try
            {
                string json = JsonConvert.SerializeObject(request);
                var resp = await EjecutarHttpAsync(() =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    return _http.PostAsync("ventas", content);
                });
                string body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    ApiEnLinea = true;
                    return (false, false, null, body.Trim('"'));
                }

                ApiEnLinea = true;
                var comprobante = JsonConvert.DeserializeObject<RegistrarVentaResponse>(body);
                if (comprobante != null)
                {
                    GuardarVentaLocalHistorial(comprobante, request.UsuarioLogin, request.Tarifa, esOffline: false);
                }
                return (true, false, comprobante, "Venta registrada en el servidor.");
            }
            catch
            {
                ApiEnLinea = false;

                var pendientes = LeerJson<List<RegistrarVentaRequest>>(_rutaVentasPendientes) ?? new List<RegistrarVentaRequest>();
                pendientes.Add(request);
                EscribirJson(_rutaVentasPendientes, pendientes);

                var cacheProd = LeerJson<List<ProductoItem>>(_rutaProductosCache) ?? new List<ProductoItem>();
                foreach (var item in itemsCarrito)
                {
                    var p = cacheProd.FirstOrDefault(x => x.IdProducto == item.IdProducto);
                    if (p != null) p.StockActual = Math.Max(0, p.StockActual - item.Cantidad);
                }
                EscribirJson(_rutaProductosCache, cacheProd);

                decimal sub = itemsCarrito.Sum(i => i.Subtotal);
                decimal iva = request.TipoDocumento == "Factura" ? Math.Round(sub * 0.15m, 2) : 0m;
                var respOffline = new RegistrarVentaResponse
                {
                    IdVenta = 0,
                    TipoDocumento = request.TipoDocumento,
                    Secuencial = $"{(request.TipoDocumento == "NotaVenta" ? "001-003" : "001-002")}-OFF{pendientes.Count:D6}",
                    ClaveAccesoSri = request.TipoDocumento == "Factura" ? "PENDIENTE-SINCRONIZACION-OFFLINE-SRI-ECUADOR-0000" : string.Empty,
                    ClienteNombre = clienteSeleccionado.RazonSocial,
                    ClienteIdentificacion = clienteSeleccionado.Identificacion,
                    ClienteDireccion = clienteSeleccionado.Direccion ?? string.Empty,
                    ClienteTelefono = clienteSeleccionado.Telefono ?? string.Empty,
                    ClienteCelular = clienteSeleccionado.Celular ?? string.Empty,
                    ClienteCorreo = clienteSeleccionado.Correo ?? string.Empty,
                    MetodoPago = request.MetodoPago,
                    ReferenciaPago = request.ReferenciaPago,
                    Subtotal = sub,
                    Iva = iva,
                    Total = Math.Round(sub + iva, 2),
                    FechaHora = DateTime.Now,
                    Items = itemsCarrito.Select(i => new ItemVentaDetalle
                    {
                        CodigoBarras = i.CodigoBarras,
                        Nombre = i.Nombre,
                        Presentacion = i.Presentacion,
                        Cantidad = i.Cantidad,
                        PrecioUnitario = i.PrecioUnitario,
                        SubtotalLinea = i.Subtotal
                    }).ToList()
                };

                GuardarVentaLocalHistorial(respOffline, request.UsuarioLogin, request.Tarifa, esOffline: true);
                return (true, true, respOffline, "Venta guardada en cola offline.");
            }
        }

        public async Task<(int Sincronizadas, int Restantes, string UltimoError)> SincronizarVentasPendientesConDetalleAsync()
        {
            var pendientes = LeerJson<List<RegistrarVentaRequest>>(_rutaVentasPendientes);
            if (pendientes == null || pendientes.Count == 0)
                return (0, 0, string.Empty);

            var restantes = new List<RegistrarVentaRequest>();
            int sincronizadas = 0;
            string ultimoError = string.Empty;

            foreach (var venta in pendientes)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(venta);
                    var resp = await EjecutarHttpAsync(() =>
                    {
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        return _http.PostAsync("ventas", content);
                    });

                    string body = await resp.Content.ReadAsStringAsync();

                    if (resp.IsSuccessStatusCode)
                    {
                        ApiEnLinea = true;
                        sincronizadas++;
                    }
                    else
                    {
                        restantes.Add(venta);
                        ultimoError = string.IsNullOrWhiteSpace(body)
                            ? $"HTTP {(int)resp.StatusCode}"
                            : body.Trim('"');
                    }
                }
                catch (Exception ex)
                {
                    ApiEnLinea = false;
                    restantes.Add(venta);
                    ultimoError = ex.Message;
                }
            }

            EscribirJson(_rutaVentasPendientes, restantes);
            return (sincronizadas, restantes.Count, ultimoError);
        }

        public async Task<int> SincronizarVentasPendientesAsync()
        {
            var res = await SincronizarVentasPendientesConDetalleAsync();
            return res.Sincronizadas;
        }

        public async Task<List<VentaHistorialItem>> ObtenerHistorialVentasAsync()
        {
            try
            {
                var resp = await EjecutarHttpAsync(() => _http.GetAsync("ventas"));
                resp.EnsureSuccessStatusCode();
                ApiEnLinea = true;
                string json = await resp.Content.ReadAsStringAsync();
                var remotos = JsonConvert.DeserializeObject<List<VentaHistorialItem>>(json) ?? new List<VentaHistorialItem>();
                if (remotos.Count > 0)
                {
                    EscribirJson(_rutaVentasLocales, remotos.Take(1000).ToList());
                }
                return remotos;
            }
            catch
            {
                ApiEnLinea = false;
                return ObtenerVentasLocales();
            }
        }

        public async Task<DashboardResumen?> ObtenerDashboardResumenAsync()
        {
            try
            {
                var resp = await EjecutarHttpAsync(() => _http.GetAsync("dashboard/resumen"));
                resp.EnsureSuccessStatusCode();
                ApiEnLinea = true;
                string json = await resp.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<DashboardResumen>(json);
            }
            catch
            {
                ApiEnLinea = false;
                return null;
            }
        }

        // ==================== IMPORTAR / EXPORTAR EXCEL (CSV UTF-8 CON ;) ====================
        public async Task<(bool Ok, int Creados, int Actualizados, string Mensaje)> ImportarInventarioDesdeArchivoAsync(string rutaArchivo)
        {
            if (!File.Exists(rutaArchivo))
                return (false, 0, 0, "El archivo seleccionado no existe.");

            var lineas = await File.ReadAllLinesAsync(rutaArchivo, Encoding.UTF8);
            if (lineas.Length <= 1)
                return (false, 0, 0, "El archivo está vacío o solo contiene encabezados.");

            char separador = lineas[0].Contains(';') ? ';' : ',';
            int creados = 0;
            int actualizados = 0;

            var existentes = await ObtenerProductosAsync(true);

            for (int i = 1; i < lineas.Length; i++)
            {
                string linea = lineas[i].Trim();
                if (string.IsNullOrWhiteSpace(linea)) continue;

                var cols = linea.Split(separador).Select(c => c.Trim().Trim('"')).ToArray();
                if (cols.Length < 2 || string.IsNullOrWhiteSpace(cols[1])) continue;

                string codigo = cols.Length > 0 && !string.IsNullOrWhiteSpace(cols[0])
                    ? cols[0]
                    : $"DEC-{DateTime.Now.Ticks % 100000:D5}";
                string nombre = cols[1];
                string presentacion = cols.Length > 2 && !string.IsNullOrWhiteSpace(cols[2]) ? cols[2] : "Unidad";

                decimal pvp = cols.Length > 3 && decimal.TryParse(cols[3].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var d1) ? d1 : 0m;
                decimal pMay = cols.Length > 4 && decimal.TryParse(cols[4].Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var d2) ? d2 : pvp;
                int stock = cols.Length > 5 && int.TryParse(cols[5].Trim(), out var s1) ? s1 : 0;

                var match = existentes.FirstOrDefault(p =>
                    string.Equals(p.CodigoBarras, codigo, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    match.CodigoBarras = codigo;
                    match.Nombre = nombre;
                    match.Presentacion = presentacion;
                    match.PrecioMinorista = pvp;
                    match.PrecioMayorista = pMay;
                    match.StockActual = stock;
                    match.Activo = true;

                    var res = await GuardarProductoAsync(match, esNuevo: false);
                    if (res.Ok) actualizados++;
                }
                else
                {
                    var nuevo = new ProductoItem
                    {
                        CodigoBarras = codigo,
                        Nombre = nombre,
                        Presentacion = presentacion,
                        PrecioMinorista = pvp,
                        PrecioMayorista = pMay,
                        StockActual = stock,
                        Activo = true
                    };

                    var res = await GuardarProductoAsync(nuevo, esNuevo: true);
                    if (res.Ok) creados++;
                }
            }

            return (true, creados, actualizados, $"Importación finalizada: {creados} creado(s), {actualizados} actualizado(s).");
        }

        public void ExportarInventarioAExcelCsv(string rutaDestino, IEnumerable<ProductoItem> productos)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CodigoBarras;Nombre;Presentacion;PrecioMinorista;PrecioMayorista;StockActual;Activo");

            foreach (var p in productos)
            {
                sb.AppendLine(string.Join(";",
                    EscapeCsv(p.CodigoBarras),
                    EscapeCsv(p.Nombre),
                    EscapeCsv(p.Presentacion),
                    p.PrecioMinorista.ToString("0.00", CultureInfo.InvariantCulture),
                    p.PrecioMayorista.ToString("0.00", CultureInfo.InvariantCulture),
                    p.StockActual,
                    p.Activo ? "SI" : "NO"));
            }

            File.WriteAllText(rutaDestino, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        public void ExportarVentasAExcelCsv(string rutaDestino, IEnumerable<VentaHistorialItem> ventas, string tituloPeriodo)
        {
            var lista = ventas.ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"Reporte de Ventas DECORLIM - {tituloPeriodo}");
            sb.AppendLine("Secuencial;FechaHora;Vendedor;Cliente;Identificacion;CorreoCliente;Tarifa;MetodoPago;Subtotal;Iva;Total;EstadoSri");

            foreach (var v in lista)
            {
                sb.AppendLine(string.Join(";",
                    EscapeCsv(v.Secuencial),
                    EscapeCsv(v.FechaHora.ToString("yyyy-MM-dd HH:mm")),
                    EscapeCsv(v.Vendedor),
                    EscapeCsv(v.Cliente),
                    EscapeCsv(v.Identificacion),
                    EscapeCsv(v.CorreoCliente),
                    EscapeCsv(v.Tarifa),
                    EscapeCsv(v.MetodoPago),
                    v.Subtotal.ToString("0.00", CultureInfo.InvariantCulture),
                    v.Iva.ToString("0.00", CultureInfo.InvariantCulture),
                    v.Total.ToString("0.00", CultureInfo.InvariantCulture),
                    EscapeCsv(v.EstadoSri)));
            }

            sb.AppendLine();
            sb.AppendLine($"TOTAL VENTAS;{lista.Count};;;;;;;{lista.Sum(x => x.Subtotal).ToString("0.00", CultureInfo.InvariantCulture)};{lista.Sum(x => x.Iva).ToString("0.00", CultureInfo.InvariantCulture)};{lista.Sum(x => x.Total).ToString("0.00", CultureInfo.InvariantCulture)};");

            File.WriteAllText(rutaDestino, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        private static string EscapeCsv(string? valor)
        {
            string s = valor ?? string.Empty;
            return s.Contains(';') || s.Contains('"') || s.Contains('\n')
                ? $"\"{s.Replace("\"", "\"\"")}\""
                : s;
        }

        // ==================== UTILIDADES JSON ====================
        private static T? LeerJson<T>(string ruta)
        {
            if (!File.Exists(ruta)) return default;
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(ruta, Encoding.UTF8));
            }
            catch
            {
                return default;
            }
        }

        private static void EscribirJson<T>(string ruta, T datos)
        {
            File.WriteAllText(ruta, JsonConvert.SerializeObject(datos, Formatting.Indented), Encoding.UTF8);
        }
    }
}