using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Program.Data;
using System.Security.Claims;
using System.Globalization;
using Npgsql;

namespace Program.Pages;

[Authorize]
public class MainModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public string? UserName { get; set; }
    public string? UserRole { get; set; }
    public int? CurrentUserId { get; set; }
    public string? SearchQuery { get; set; }
    public string ActiveTab { get; set; } = "products";
    
    // параметры фильтрации
    public string? FilterActive { get; set; } // "all", "active", "inactive"
    public int? FilterRoleId { get; set; }
    public int? FilterSellerId { get; set; }
    public int? FilterMasterId { get; set; }
    public DateTime? FilterDateFrom { get; set; }
    public DateTime? FilterDateTo { get; set; }

    public List<Models.Product> Products { get; set; } = new();
    public List<Models.Service> Services { get; set; } = new();
    public List<Models.User> Users { get; set; } = new();
    public List<Models.Role> Roles { get; set; } = new();
    public List<Models.Sale> Sales { get; set; } = new();
    public List<Models.ServiceOrder> ServiceOrders { get; set; } = new();
    public List<Models.Supply> Supplies { get; set; } = new();
    
    // товары с низким остатком (меньше 5 единиц)
    public List<Models.Product> LowStockProducts { get; set; } = new();
    
    // отчёты
    public string? ReportType { get; set; } = "sales";
    public ReportDataModel? ReportData { get; set; }

    public MainModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(string? tab = "products", string? search = null, string? action = null, int? id = null,
        string? filterActive = null, int? filterRoleId = null, int? filterSellerId = null, int? filterMasterId = null,
        DateTime? filterDateFrom = null, DateTime? filterDateTo = null, string? reportType = null)
    {
        ActiveTab = tab ?? "products";
        SearchQuery = search;
        FilterActive = filterActive;
        FilterRoleId = filterRoleId;
        FilterSellerId = filterSellerId;
        FilterMasterId = filterMasterId;
        
        // Преобразуем даты в UTC для PostgreSQL
        // Даты из формы приходят как Unspecified, преобразуем в UTC (начало дня)
        if (filterDateFrom.HasValue)
        {
            var dateFrom = filterDateFrom.Value.Date;
            // Преобразуем в UTC, считая что дата указана в локальном времени
            FilterDateFrom = DateTime.SpecifyKind(dateFrom, DateTimeKind.Utc);
        }
        if (filterDateTo.HasValue)
        {
            var dateTo = filterDateTo.Value.Date;
            // Преобразуем в UTC, считая что дата указана в локальном времени
            FilterDateTo = DateTime.SpecifyKind(dateTo, DateTimeKind.Utc);
        }

        UserName = User.Identity?.Name;
        UserRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdClaim, out var userId))
        {
            CurrentUserId = userId;
        }

        // Обработка удаления
        if (action == "delete" && id.HasValue)
        {
            await HandleDeleteAsync(ActiveTab, id.Value);
            // Редирект после удаления, чтобы избежать повторного удаления при обновлении страницы
            var redirectUrl = $"/Main?tab={ActiveTab}";
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                redirectUrl += $"&search={Uri.EscapeDataString(SearchQuery)}";
            }
            if (!string.IsNullOrWhiteSpace(FilterActive))
            {
                redirectUrl += $"&filterActive={FilterActive}";
            }
            if (FilterRoleId.HasValue)
            {
                redirectUrl += $"&filterRoleId={FilterRoleId.Value}";
            }
            if (FilterSellerId.HasValue)
            {
                redirectUrl += $"&filterSellerId={FilterSellerId.Value}";
            }
            if (FilterMasterId.HasValue)
            {
                redirectUrl += $"&filterMasterId={FilterMasterId.Value}";
            }
            if (FilterDateFrom.HasValue)
            {
                redirectUrl += $"&filterDateFrom={FilterDateFrom.Value:yyyy-MM-dd}";
            }
            if (FilterDateTo.HasValue)
            {
                redirectUrl += $"&filterDateTo={FilterDateTo.Value:yyyy-MM-dd}";
            }
            return Redirect(redirectUrl);
        }

        await LoadDataAsync();
        
        // Загружаем отчёты, если открыта вкладка отчётов
        if (ActiveTab == "reports")
        {
            ReportType = reportType ?? "sales";
            await LoadReportDataAsync();
        }
        
        return Page();
    }

    private async Task HandleDeleteAsync(string tab, int id)
    {
        try
        {
            switch (tab.ToLower())
            {
                case "products":
                    var product = await _context.Products.FindAsync(id);
                    if (product != null)
                    {
                        _context.Products.Remove(product);
                        await _context.SaveChangesAsync();
                    }
                    break;

                case "services":
                    var service = await _context.Services.FindAsync(id);
                    if (service != null)
                    {
                        _context.Services.Remove(service);
                        await _context.SaveChangesAsync();
                    }
                    break;

                case "users":
                    var user = await _context.Users.FindAsync(id);
                    if (user != null)
                    {
                        _context.Users.Remove(user);
                        await _context.SaveChangesAsync();
                    }
                    break;

                case "roles":
                    var role = await _context.Roles.FindAsync(id);
                    if (role != null)
                    {
                        // Проверяем, нет ли пользователей с этой ролью
                        var usersWithRole = await _context.Users.AnyAsync(u => u.RoleId == id);
                        if (usersWithRole)
                        {
                            // Можно либо запретить удаление, либо установить роль в null
                            // Пока просто запрещаем удаление
                            return;
                        }
                        _context.Roles.Remove(role);
                        await _context.SaveChangesAsync();
                    }
                    break;

                case "serviceorders":
                    var serviceOrder = await _context.ServiceOrders.FindAsync(id);
                    if (serviceOrder != null)
                    {
                        _context.ServiceOrders.Remove(serviceOrder);
                        await _context.SaveChangesAsync();
                    }
                    break;
            }
        }
        catch (Exception)
        {
            // Обработка ошибок (можно добавить логирование)
        }
    }

    private async Task LoadDataAsync()
    {
        var query = SearchQuery?.ToLower() ?? string.Empty;

        // Всегда загружаем роли для выпадающего списка в форме пользователя
        Roles = await _context.Roles.OrderBy(r => r.RoleName).ToListAsync();

        switch (ActiveTab.ToLower())
        {
            case "products":
                var productsQuery = _context.Products.AsQueryable();
                if (!string.IsNullOrWhiteSpace(query))
                {
                    productsQuery = productsQuery.Where(p =>
                        p.Name.ToLower().Contains(query) ||
                        (p.Description != null && p.Description.ToLower().Contains(query)));
                }
                // Фильтр по статусу активности
                if (FilterActive == "active")
                {
                    productsQuery = productsQuery.Where(p => p.IsActive);
                }
                else if (FilterActive == "inactive")
                {
                    productsQuery = productsQuery.Where(p => !p.IsActive);
                }
                Products = await productsQuery.OrderBy(p => p.Name).ToListAsync();
                break;

            case "services":
                var servicesQuery = _context.Services.AsQueryable();
                if (!string.IsNullOrWhiteSpace(query))
                {
                    servicesQuery = servicesQuery.Where(s =>
                        s.Name.ToLower().Contains(query) ||
                        (s.Description != null && s.Description.ToLower().Contains(query)));
                }
                // Фильтр по статусу активности
                if (FilterActive == "active")
                {
                    servicesQuery = servicesQuery.Where(s => s.IsActive);
                }
                else if (FilterActive == "inactive")
                {
                    servicesQuery = servicesQuery.Where(s => !s.IsActive);
                }
                Services = await servicesQuery.OrderBy(s => s.Name).ToListAsync();
                break;

            case "users":
                var usersQuery = _context.Users.Include(u => u.Role).AsQueryable();
                if (!string.IsNullOrWhiteSpace(query))
                {
                    usersQuery = usersQuery.Where(u =>
                        u.Login.ToLower().Contains(query) ||
                        u.FullName.ToLower().Contains(query) ||
                        (u.Phone != null && u.Phone.Contains(query)));
                }
                // Фильтр по статусу активности
                if (FilterActive == "active")
                {
                    usersQuery = usersQuery.Where(u => u.IsActive);
                }
                else if (FilterActive == "inactive")
                {
                    usersQuery = usersQuery.Where(u => !u.IsActive);
                }
                // Фильтр по роли
                if (FilterRoleId.HasValue && FilterRoleId.Value > 0)
                {
                    usersQuery = usersQuery.Where(u => u.RoleId == FilterRoleId.Value);
                }
                Users = await usersQuery.OrderBy(u => u.FullName).ToListAsync();
                break;

            case "roles":
                var rolesQuery = _context.Roles.AsQueryable();
                if (!string.IsNullOrWhiteSpace(query))
                {
                    rolesQuery = rolesQuery.Where(r => r.RoleName.ToLower().Contains(query));
                }
                Roles = rolesQuery.OrderBy(r => r.RoleName).ToList();
                break;

                case "sales":
                    var salesQuery = _context.Sales
                        .Include(s => s.Seller)
                        .Include(s => s.SaleItems)
                            .ThenInclude(si => si.Product)
                        .AsQueryable();
                    // Фильтр по продавцу
                    if (FilterSellerId.HasValue && FilterSellerId.Value > 0)
                    {
                        salesQuery = salesQuery.Where(s => s.SellerId == FilterSellerId.Value);
                    }
                    // Фильтр по дате (даты уже в UTC)
                    if (FilterDateFrom.HasValue)
                    {
                        var dateFrom = FilterDateFrom.Value;
                        salesQuery = salesQuery.Where(s => s.SaleDate >= dateFrom);
                    }
                    if (FilterDateTo.HasValue)
                    {
                        var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
                        salesQuery = salesQuery.Where(s => s.SaleDate <= dateTo);
                    }
                    Sales = await salesQuery.OrderByDescending(s => s.SaleDate).ToListAsync();
                    // Загружаем активные товары для формы продажи
                    Products = await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
                    // Загружаем пользователей для фильтра продавцов
                    Users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
                    break;

                case "serviceorders":
                    var serviceOrdersQuery = _context.ServiceOrders
                        .Include(so => so.Master)
                        .Include(so => so.ServiceOrderItems)
                            .ThenInclude(soi => soi.Service)
                        .AsQueryable();
                    // Фильтр по мастеру
                    if (FilterMasterId.HasValue && FilterMasterId.Value > 0)
                    {
                        serviceOrdersQuery = serviceOrdersQuery.Where(so => so.MasterId == FilterMasterId.Value);
                    }
                    // Фильтр по дате (даты уже в UTC)
                    if (FilterDateFrom.HasValue)
                    {
                        var dateFrom = FilterDateFrom.Value;
                        serviceOrdersQuery = serviceOrdersQuery.Where(so => so.OrderDate >= dateFrom);
                    }
                    if (FilterDateTo.HasValue)
                    {
                        var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
                        serviceOrdersQuery = serviceOrdersQuery.Where(so => so.OrderDate <= dateTo);
                    }
                    ServiceOrders = await serviceOrdersQuery.OrderByDescending(so => so.OrderDate).ToListAsync();
                    // Загружаем активные услуги для формы заказа
                    Services = await _context.Services.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
                    // Загружаем пользователей для фильтра мастеров
                    Users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
                    break;

                case "supplies":
                    var suppliesQuery = _context.Supplies
                        .Include(s => s.ReceivedByUser)
                        .Include(s => s.SupplyItems)
                            .ThenInclude(si => si.Product)
                        .AsQueryable();
                    // Фильтр по дате
                    if (FilterDateFrom.HasValue)
                    {
                        var dateFrom = FilterDateFrom.Value;
                        suppliesQuery = suppliesQuery.Where(s => s.SupplyDate >= dateFrom);
                    }
                    if (FilterDateTo.HasValue)
                    {
                        var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
                        suppliesQuery = suppliesQuery.Where(s => s.SupplyDate <= dateTo);
                    }
                    Supplies = await suppliesQuery.OrderByDescending(s => s.SupplyDate).ToListAsync();
                    // Загружаем активные товары для формы поставки
                    Products = await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
                    break;

                case "reports":
                    // Загружаем данные для отчётов
                    var reportsSalesQuery = _context.Sales
                        .Include(s => s.Seller)
                        .Include(s => s.SaleItems)
                            .ThenInclude(si => si.Product)
                        .AsQueryable();
                    if (FilterDateFrom.HasValue)
                    {
                        reportsSalesQuery = reportsSalesQuery.Where(s => s.SaleDate >= FilterDateFrom.Value);
                    }
                    if (FilterDateTo.HasValue)
                    {
                        var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
                        reportsSalesQuery = reportsSalesQuery.Where(s => s.SaleDate <= dateTo);
                    }
                    Sales = await reportsSalesQuery.OrderByDescending(s => s.SaleDate).ToListAsync();
                    
                    var reportsServiceOrdersQuery = _context.ServiceOrders
                        .Include(so => so.Master)
                        .Include(so => so.ServiceOrderItems)
                            .ThenInclude(soi => soi.Service)
                        .AsQueryable();
                    if (FilterDateFrom.HasValue)
                    {
                        reportsServiceOrdersQuery = reportsServiceOrdersQuery.Where(so => so.OrderDate >= FilterDateFrom.Value);
                    }
                    if (FilterDateTo.HasValue)
                    {
                        var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
                        reportsServiceOrdersQuery = reportsServiceOrdersQuery.Where(so => so.OrderDate <= dateTo);
                    }
                    ServiceOrders = await reportsServiceOrdersQuery.OrderByDescending(so => so.OrderDate).ToListAsync();
                    
                    var reportsSuppliesQuery = _context.Supplies
                        .Include(s => s.ReceivedByUser)
                        .Include(s => s.SupplyItems)
                            .ThenInclude(si => si.Product)
                        .AsQueryable();
                    if (FilterDateFrom.HasValue)
                    {
                        reportsSuppliesQuery = reportsSuppliesQuery.Where(s => s.SupplyDate >= FilterDateFrom.Value);
                    }
                    if (FilterDateTo.HasValue)
                    {
                        var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
                        reportsSuppliesQuery = reportsSuppliesQuery.Where(s => s.SupplyDate <= dateTo);
                    }
                    Supplies = await reportsSuppliesQuery.OrderByDescending(s => s.SupplyDate).ToListAsync();
                    
                    Products = await _context.Products.OrderBy(p => p.Name).ToListAsync();
                    Users = await _context.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
                    break;
        }
        
        // Если товары еще не загружены (для других вкладок), загружаем активные товары для формы продажи
        if (!Products.Any() && ActiveTab != "products")
        {
            Products = await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        }
        
        // Если услуги еще не загружены (для других вкладок), загружаем активные услуги для формы заказа
        if (!Services.Any() && ActiveTab != "services" && ActiveTab != "serviceorders")
        {
            Services = await _context.Services.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        }
        
        // Загружаем товары с низким остатком (меньше 5 единиц) для уведомлений
        LowStockProducts = await _context.Products
            .Where(p => p.IsActive && p.StockQuantity < 5 && p.StockQuantity >= 0)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    // Обработчики сохранения
    public async Task<IActionResult> OnPostSaveProduct(int? ProductId, string Name, string? Description, string? Price, string? CostPrice, int StockQuantity, string? IsActive)
    {
        // Парсим цену продажи, заменяя запятую на точку для поддержки разных локалей
        decimal parsedPrice = 0;
        if (!string.IsNullOrWhiteSpace(Price))
        {
            var normalizedPrice = Price.Replace(',', '.');
            decimal.TryParse(normalizedPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedPrice);
        }

        // Парсим закупочную цену (только для директора)
        decimal parsedCostPrice = 0;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        bool isDirector = userRole == "Директор" || userRole == "Заместитель директора";
        
        if (isDirector && !string.IsNullOrWhiteSpace(CostPrice))
        {
            var normalizedCostPrice = CostPrice.Replace(',', '.');
            decimal.TryParse(normalizedCostPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedCostPrice);
        }

        if (string.IsNullOrWhiteSpace(Name) || parsedPrice < 0)
        {
            return RedirectToPage("/Main", new { tab = "products" });
        }

        if (ProductId.HasValue && ProductId.Value > 0)
        {
            // Редактирование
            var product = await _context.Products.FindAsync(ProductId.Value);
            if (product != null)
            {
                product.Name = Name;
                product.Description = Description;
                product.Price = parsedPrice;
                if (isDirector)
                {
                    product.CostPrice = parsedCostPrice;
                }
                product.StockQuantity = StockQuantity;
                product.IsActive = IsActive == "true" || IsActive == "True";
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // Создание
            var product = new Models.Product
            {
                Name = Name,
                Description = Description,
                Price = parsedPrice,
                CostPrice = isDirector ? parsedCostPrice : 0,
                StockQuantity = StockQuantity,
                IsActive = IsActive == "true" || IsActive == "True"
            };
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("/Main", new { tab = "products" });
    }

    public async Task<IActionResult> OnPostSaveService(int? ServiceId, string Name, string? Description, string? Price, string? IsActive)
    {
        // Парсим цену, заменяя запятую на точку для поддержки разных локалей
        decimal parsedPrice = 0;
        if (!string.IsNullOrWhiteSpace(Price))
        {
            var normalizedPrice = Price.Replace(',', '.');
            decimal.TryParse(normalizedPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedPrice);
        }

        if (string.IsNullOrWhiteSpace(Name) || parsedPrice < 0)
        {
            return RedirectToPage("/Main", new { tab = "services" });
        }

        if (ServiceId.HasValue && ServiceId.Value > 0)
        {
            // Редактирование
            var service = await _context.Services.FindAsync(ServiceId.Value);
            if (service != null)
            {
                service.Name = Name;
                service.Description = Description;
                service.Price = parsedPrice;
                service.IsActive = IsActive == "true" || IsActive == "True";
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // Создание
            var service = new Models.Service
            {
                Name = Name,
                Description = Description,
                Price = parsedPrice,
                IsActive = IsActive == "true" || IsActive == "True"
            };
            _context.Services.Add(service);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("/Main", new { tab = "services" });
    }

    public async Task<IActionResult> OnPostSaveUser(int? UserId, string Login, string? Password, string FullName, string? Phone, int RoleId, string? IsActive)
    {
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(FullName) || RoleId <= 0)
        {
            return RedirectToPage("/Main", new { tab = "users" });
        }

        if (UserId.HasValue && UserId.Value > 0)
        {
            // Редактирование
            var user = await _context.Users.FindAsync(UserId.Value);
            if (user != null)
            {
                user.Login = Login;
                user.FullName = FullName;
                user.Phone = Phone;
                user.RoleId = RoleId;
                user.IsActive = IsActive == "true" || IsActive == "True";
                
                // Обновляем пароль только если он указан
                if (!string.IsNullOrWhiteSpace(Password))
                {
                    user.PasswordHash = Password; // В продакшене здесь должно быть хеширование
                }
                
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // Создание
            if (string.IsNullOrWhiteSpace(Password))
            {
                return RedirectToPage("/Main", new { tab = "users" });
            }

            // Проверяем, не существует ли уже пользователь с таким логином
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Login == Login);
            if (existingUser != null)
            {
                return RedirectToPage("/Main", new { tab = "users" });
            }

            var user = new Models.User
            {
                Login = Login,
                PasswordHash = Password, // В продакшене здесь должно быть хеширование
                FullName = FullName,
                Phone = Phone,
                RoleId = RoleId,
                IsActive = IsActive == "true" || IsActive == "True"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("/Main", new { tab = "users" });
    }

    public async Task<IActionResult> OnPostSaveRole(int? RoleId, string RoleName)
    {
        if (string.IsNullOrWhiteSpace(RoleName))
        {
            return RedirectToPage("/Main", new { tab = "roles" });
        }

        if (RoleId.HasValue && RoleId.Value > 0)
        {
            // Редактирование
            var role = await _context.Roles.FindAsync(RoleId.Value);
            if (role != null)
            {
                role.RoleName = RoleName;
                await _context.SaveChangesAsync();
            }
        }
        else
        {
            // Создание
            // Проверяем, не существует ли уже роль с таким именем
            var existingRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleName);
            if (existingRole != null)
            {
                return RedirectToPage("/Main", new { tab = "roles" });
            }

            var role = new Models.Role
            {
                RoleName = RoleName
            };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage("/Main", new { tab = "roles" });
    }

    [BindProperty]
    public List<SaleItemData> SaleItems { get; set; } = new();

    public async Task<IActionResult> OnPostCreateSale()
    {
        // Получаем ID текущего пользователя
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var sellerId) || sellerId <= 0)
        {
            return RedirectToPage("/Main", new { tab = "sales" });
        }

        // Всегда получаем данные из Request.Form, так как привязка модели может не работать
        var form = Request.Form;
        var indices = new List<int>();
        
        // Ищем все индексы, которые есть в форме
        for (int i = 0; i < 100; i++) // Проверяем до 100 элементов
        {
            if (form.ContainsKey($"SaleItems[{i}].ProductId"))
            {
                var productIdValue = form[$"SaleItems[{i}].ProductId"].ToString();
                if (!string.IsNullOrWhiteSpace(productIdValue) && productIdValue != "0")
                {
                    indices.Add(i);
                }
            }
        }
        
        if (indices.Count == 0)
        {
            return RedirectToPage("/Main", new { tab = "sales" });
        }
        
        // Создаем список товаров из формы
        SaleItems = new List<SaleItemData>();
        foreach (var i in indices)
        {
            var productIdStr = form[$"SaleItems[{i}].ProductId"].ToString();
            var quantityStr = form[$"SaleItems[{i}].Quantity"].ToString();
            var priceStr = form[$"SaleItems[{i}].PriceAtSale"].ToString();
            
            if (int.TryParse(productIdStr, out var productId) &&
                int.TryParse(quantityStr, out var quantity) &&
                decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price))
            {
                if (productId > 0 && quantity > 0 && price > 0)
                {
                    SaleItems.Add(new SaleItemData
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        PriceAtSale = price
                    });
                }
            }
        }

        // Фильтруем только валидные товары (убираем пустые элементы)
        var validItems = SaleItems
            .Where(item => item != null && item.ProductId > 0 && item.Quantity > 0 && item.PriceAtSale > 0)
            .ToList();
        
        if (!validItems.Any())
        {
            return RedirectToPage("/Main", new { tab = "sales" });
        }

        try
        {
            // Используем транзакцию для атомарности операций
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Проверяем наличие товаров на складе перед созданием продажи
                foreach (var itemData in validItems)
                {
                    var product = await _context.Products.FindAsync(itemData.ProductId);
                    if (product == null || !product.IsActive)
                    {
                        // Товар не найден или неактивен
                        await transaction.RollbackAsync();
                        return RedirectToPage("/Main", new { tab = "sales" });
                    }
                    if (product.StockQuantity < itemData.Quantity)
                    {
                        // Недостаточно товара на складе
                        await transaction.RollbackAsync();
                        return RedirectToPage("/Main", new { tab = "sales" });
                    }
                }

                // Создаем продажу
                var sale = new Models.Sale
                {
                    SellerId = sellerId,
                    SaleDate = DateTime.Now,
                    TotalAmount = 0 // Будет рассчитано триггером
                };
                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Добавляем товары в продажу
                // ВАЖНО: Уменьшение количества товара на складе выполняется триггером в базе данных
                // (trg_decrease_stock), поэтому здесь мы только создаем записи SaleItem
                foreach (var itemData in validItems)
                {
                    var saleItem = new Models.SaleItem
                    {
                        SaleId = sale.SaleId,
                        ProductId = itemData.ProductId,
                        Quantity = itemData.Quantity,
                        PriceAtSale = itemData.PriceAtSale
                    };
                    _context.SaleItems.Add(saleItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Редирект с сохранением фильтров, если они были установлены
                var redirectParams = new Dictionary<string, string> { { "tab", "sales" } };
                if (Request.Query.ContainsKey("filterSellerId"))
                {
                    redirectParams["filterSellerId"] = Request.Query["filterSellerId"].ToString();
                }
                if (Request.Query.ContainsKey("filterDateFrom"))
                {
                    redirectParams["filterDateFrom"] = Request.Query["filterDateFrom"].ToString();
                }
                if (Request.Query.ContainsKey("filterDateTo"))
                {
                    redirectParams["filterDateTo"] = Request.Query["filterDateTo"].ToString();
                }
                
                return RedirectToPage("/Main", redirectParams);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "P0001")
        {
            // Обработка исключений от триггеров PostgreSQL (RAISE EXCEPTION)
            // В продакшене здесь должно быть логирование
            // Временно выводим ошибку в консоль для отладки
            System.Diagnostics.Debug.WriteLine($"PostgreSQL Exception: {pgEx.Message}");
            return RedirectToPage("/Main", new { tab = "sales" });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            // Обработка ошибок базы данных
            // Временно выводим ошибку в консоль для отладки
            System.Diagnostics.Debug.WriteLine($"DbUpdateException: {dbEx.Message}");
            if (dbEx.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {dbEx.InnerException.Message}");
            }
            return RedirectToPage("/Main", new { tab = "sales" });
        }
        catch (Exception ex)
        {
            // Обработка других ошибок
            // Временно выводим ошибку в консоль для отладки
            System.Diagnostics.Debug.WriteLine($"Exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            return RedirectToPage("/Main", new { tab = "sales" });
        }
    }

    public async Task<IActionResult> OnGetSaleDetails(int id)
    {
        var sale = await _context.Sales
            .Include(s => s.Seller)
            .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
            .FirstOrDefaultAsync(s => s.SaleId == id);

        if (sale == null)
        {
            return Content("<p>Продажа не найдена</p>");
        }

        var saleDateDisplay = sale.SaleDate;
        var html = $@"
            <div class='mb-3'>
                <strong>Дата:</strong> {saleDateDisplay:dd.MM.yyyy HH:mm}
            </div>
            <div class='mb-3'>
                <strong>Продавец:</strong> {sale.Seller?.FullName ?? "-"}
            </div>
            <div class='mb-3'>
                <strong>Сумма:</strong> {sale.TotalAmount:C}
            </div>
            <table class='table table-striped'>
                <thead>
                    <tr>
                        <th>Товар</th>
                        <th>Количество</th>
                        <th>Цена за шт.</th>
                        <th>Сумма</th>
                    </tr>
                </thead>
                <tbody>
                    {string.Join("", sale.SaleItems.Select(si => $@"
                        <tr>
                            <td>{si.Product?.Name ?? "-"}</td>
                            <td>{si.Quantity}</td>
                            <td>{si.PriceAtSale:C}</td>
                            <td>{(si.Quantity * si.PriceAtSale):C}</td>
                        </tr>
                    "))}
                </tbody>
            </table>
        ";

        return Content(html, "text/html");
    }

    [BindProperty]
    public List<ServiceOrderItemData> ServiceOrderItems { get; set; } = new();

    public async Task<IActionResult> OnPostCreateServiceOrder(string? Notes)
    {
        // Получаем ID текущего пользователя
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var masterId) || masterId <= 0)
        {
            return RedirectToPage("/Main", new { tab = "serviceorders" });
        }

        // Проверяем, что данные пришли
        if (ServiceOrderItems == null || ServiceOrderItems.Count == 0)
        {
            // Пробуем получить данные из Request.Form напрямую
            var form = Request.Form;
            var serviceOrderItemsCount = 0;
            
            // Ищем все индексы, которые есть в форме
            var indices = new List<int>();
            for (int i = 0; i < 100; i++) // Проверяем до 100 элементов
            {
                if (form.ContainsKey($"ServiceOrderItems[{i}].ServiceId"))
                {
                    var serviceIdValue = form[$"ServiceOrderItems[{i}].ServiceId"].ToString();
                    if (!string.IsNullOrWhiteSpace(serviceIdValue) && serviceIdValue != "0")
                    {
                        indices.Add(i);
                        serviceOrderItemsCount++;
                    }
                }
            }
            
            if (serviceOrderItemsCount == 0)
            {
                return RedirectToPage("/Main", new { tab = "serviceorders" });
            }
            
            // Если данные есть в форме, но не привязались, создаем список вручную
            ServiceOrderItems = new List<ServiceOrderItemData>();
            foreach (var i in indices)
            {
                if (int.TryParse(form[$"ServiceOrderItems[{i}].ServiceId"], out var serviceId))
                {
                    if (serviceId > 0)
                    {
                        ServiceOrderItems.Add(new ServiceOrderItemData
                        {
                            ServiceId = serviceId
                        });
                    }
                }
            }
        }

        // Фильтруем только валидные услуги (убираем пустые элементы)
        var validItems = ServiceOrderItems
            .Where(item => item != null && item.ServiceId > 0)
            .ToList();
        
        if (!validItems.Any())
        {
            return RedirectToPage("/Main", new { tab = "serviceorders" });
        }

        try
        {
            // Используем транзакцию для атомарности операций
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Проверяем наличие услуг перед созданием заказа
                foreach (var itemData in validItems)
                {
                    var service = await _context.Services.FindAsync(itemData.ServiceId);
                    if (service == null || !service.IsActive)
                    {
                        // Услуга не найдена или неактивна
                        await transaction.RollbackAsync();
                        return RedirectToPage("/Main", new { tab = "serviceorders" });
                    }
                }

                // Создаем заказ услуг
                var serviceOrder = new Models.ServiceOrder
                {
                    MasterId = masterId,
                    OrderDate = DateTime.Now,
                    TotalAmount = 0, // Будет рассчитано триггером
                    Notes = Notes
                };
                _context.ServiceOrders.Add(serviceOrder);
                await _context.SaveChangesAsync();

                // Добавляем услуги в заказ
                foreach (var itemData in validItems)
                {
                    var serviceOrderItem = new Models.ServiceOrderItem
                    {
                        OrderId = serviceOrder.OrderId,
                        ServiceId = itemData.ServiceId
                        // PriceAtOrder будет установлен триггером
                    };
                    _context.ServiceOrderItems.Add(serviceOrderItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToPage("/Main", new { tab = "serviceorders" });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "P0001")
        {
            // Обработка исключений от триггеров PostgreSQL (RAISE EXCEPTION)
            // В продакшене здесь должно быть логирование
            return RedirectToPage("/Main", new { tab = "serviceorders" });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Обработка ошибок базы данных
            // В продакшене здесь должно быть логирование
            return RedirectToPage("/Main", new { tab = "serviceorders" });
        }
        catch (Exception)
        {
            // Обработка других ошибок
            // В продакшене здесь должно быть логирование
            return RedirectToPage("/Main", new { tab = "serviceorders" });
        }
    }

    public async Task<IActionResult> OnGetServiceOrderDetails(int id)
    {
        var serviceOrder = await _context.ServiceOrders
            .Include(so => so.Master)
            .Include(so => so.ServiceOrderItems)
                .ThenInclude(soi => soi.Service)
            .FirstOrDefaultAsync(so => so.OrderId == id);

        if (serviceOrder == null)
        {
            return Content("<p>Заказ услуг не найден</p>");
        }

        var orderDateDisplay = serviceOrder.OrderDate;
        var html = $@"
            <div class='mb-3'>
                <strong>Дата:</strong> {orderDateDisplay:dd.MM.yyyy HH:mm}
            </div>
            <div class='mb-3'>
                <strong>Мастер:</strong> {serviceOrder.Master?.FullName ?? "-"}
            </div>
            <div class='mb-3'>
                <strong>Сумма:</strong> {(serviceOrder.TotalAmount ?? 0):C}
            </div>
            {(string.IsNullOrWhiteSpace(serviceOrder.Notes) ? "" : $@"
            <div class='mb-3'>
                <strong>Примечания:</strong> {serviceOrder.Notes}
            </div>")}
            <table class='table table-striped'>
                <thead>
                    <tr>
                        <th>Услуга</th>
                        <th>Цена</th>
                    </tr>
                </thead>
                <tbody>
                    {string.Join("", serviceOrder.ServiceOrderItems.Select(soi => $@"
                        <tr>
                            <td>{soi.Service?.Name ?? "-"}</td>
                            <td>{soi.PriceAtOrder:C}</td>
                        </tr>
                    "))}
                </tbody>
            </table>
        ";

        return Content(html, "text/html");
    }

    public class SaleItemData
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal PriceAtSale { get; set; }
    }

    public class ServiceOrderItemData
    {
        public int ServiceId { get; set; }
    }

    public class SupplyItemData
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal CostPrice { get; set; }
    }

    [BindProperty]
    public List<SupplyItemData> SupplyItems { get; set; } = new();

    public async Task<IActionResult> OnPostCreateSupply(string? SupplierName, string? Notes)
    {
        System.Diagnostics.Debug.WriteLine($"=== OnPostCreateSupply START ===");
        System.Diagnostics.Debug.WriteLine($"SupplierName: {SupplierName}, Notes: {Notes}");
        
        // Получаем ID текущего пользователя
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        System.Diagnostics.Debug.WriteLine($"UserIdClaim: {userIdClaim}");
        
        if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
        {
            System.Diagnostics.Debug.WriteLine("User not found, redirecting...");
            return RedirectToPage("/Main", new { tab = "supplies" });
        }

        // Проверяем роль - только директор видит закупочные цены
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        bool isDirector = userRole == "Директор" || userRole == "Заместитель директора";
        System.Diagnostics.Debug.WriteLine($"UserRole: {userRole}, IsDirector: {isDirector}");

        // Получаем данные из формы
        var form = Request.Form;
        System.Diagnostics.Debug.WriteLine($"Form keys: {string.Join(", ", form.Keys)}");
        
        var indices = new List<int>();
        
        for (int i = 0; i < 100; i++)
        {
            if (form.ContainsKey($"SupplyItems[{i}].ProductId"))
            {
                var productIdValue = form[$"SupplyItems[{i}].ProductId"].ToString();
                System.Diagnostics.Debug.WriteLine($"Found SupplyItems[{i}].ProductId = {productIdValue}");
                if (!string.IsNullOrWhiteSpace(productIdValue) && productIdValue != "0")
                {
                    indices.Add(i);
                }
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"Indices count: {indices.Count}");
        
        if (indices.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("No items found, redirecting...");
            return RedirectToPage("/Main", new { tab = "supplies" });
        }
        
        // Создаем список товаров из формы
        SupplyItems = new List<SupplyItemData>();
        foreach (var i in indices)
        {
            var productIdStr = form[$"SupplyItems[{i}].ProductId"].ToString();
            var quantityStr = form[$"SupplyItems[{i}].Quantity"].ToString();
            var costPriceStr = form[$"SupplyItems[{i}].CostPrice"].ToString();
            
            if (int.TryParse(productIdStr, out var productId) &&
                int.TryParse(quantityStr, out var quantity))
            {
                if (productId > 0 && quantity > 0)
                {
                    decimal costPrice = 0;
                    // Если директор - берём цену из формы, иначе из карточки товара
                    if (isDirector && !string.IsNullOrWhiteSpace(costPriceStr))
                    {
                        decimal.TryParse(costPriceStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out costPrice);
                    }
                    
                    SupplyItems.Add(new SupplyItemData
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        CostPrice = costPrice
                    });
                }
            }
        }

        var validItems = SupplyItems
            .Where(item => item != null && item.ProductId > 0 && item.Quantity > 0)
            .ToList();
        
        if (!validItems.Any())
        {
            return RedirectToPage("/Main", new { tab = "supplies" });
        }

        try
        {
            // Используем ExecutionStrategy для работы с транзакциями
            var strategy = _context.Database.CreateExecutionStrategy();
            
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                try
                {
                    // Создаем поставку
                    var supply = new Models.Supply
                    {
                        SupplierName = SupplierName,
                        ReceivedByUserId = userId,
                        SupplyDate = DateTime.UtcNow,
                        TotalCost = 0,
                        Notes = Notes
                    };
                    _context.Supplies.Add(supply);
                    await _context.SaveChangesAsync();

                    decimal totalCost = 0;

                    // Добавляем товары в поставку
                    foreach (var itemData in validItems)
                    {
                        var product = await _context.Products.FindAsync(itemData.ProductId);
                        if (product == null) continue;

                        // Если закупочная цена не указана (продавец), берём из карточки товара
                        decimal costPrice = itemData.CostPrice > 0 ? itemData.CostPrice : product.CostPrice;

                        var supplyItem = new Models.SupplyItem
                        {
                            SupplyId = supply.SupplyId,
                            ProductId = itemData.ProductId,
                            Quantity = itemData.Quantity,
                            CostPriceAtSupply = costPrice
                        };
                        _context.SupplyItems.Add(supplyItem);

                        // Увеличиваем количество товара на складе
                        product.StockQuantity += itemData.Quantity;

                        // Если директор указал новую закупочную цену - обновляем в карточке товара
                        if (isDirector && itemData.CostPrice > 0)
                        {
                            product.CostPrice = itemData.CostPrice;
                        }

                        totalCost += costPrice * itemData.Quantity;
                    }

                    // Обновляем общую сумму поставки
                    supply.TotalCost = totalCost;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            return RedirectToPage("/Main", new { tab = "supplies" });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"!!! SUPPLY ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            TempData["Error"] = $"Ошибка при создании поставки: {ex.Message}";
            return RedirectToPage("/Main", new { tab = "supplies" });
        }
    }

    public async Task<IActionResult> OnGetSupplyDetails(int id)
    {
        var supply = await _context.Supplies
            .Include(s => s.ReceivedByUser)
            .Include(s => s.SupplyItems)
                .ThenInclude(si => si.Product)
            .FirstOrDefaultAsync(s => s.SupplyId == id);

        if (supply == null)
        {
            return Content("<p>Поставка не найдена</p>");
        }

        // Проверяем роль - только директор видит закупочные цены
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        bool isDirector = userRole == "Директор" || userRole == "Заместитель директора";

        var supplyDateDisplay = supply.SupplyDate;
        
        string tableContent;
        if (isDirector)
        {
            tableContent = $@"
                <table class='table table-striped'>
                    <thead>
                        <tr>
                            <th>Товар</th>
                            <th>Количество</th>
                            <th>Закуп. цена</th>
                            <th>Сумма</th>
                        </tr>
                    </thead>
                    <tbody>
                        {string.Join("", supply.SupplyItems.Select(si => $@"
                            <tr>
                                <td>{si.Product?.Name ?? "-"}</td>
                                <td>{si.Quantity} шт.</td>
                                <td>{si.CostPriceAtSupply:N2} ₽</td>
                                <td>{(si.Quantity * si.CostPriceAtSupply):N2} ₽</td>
                            </tr>
                        "))}
                    </tbody>
                    <tfoot>
                        <tr>
                            <th colspan='3'>Итого:</th>
                            <th>{supply.TotalCost:N2} ₽</th>
                        </tr>
                    </tfoot>
                </table>";
        }
        else
        {
            // Для продавцов - без цен
            tableContent = $@"
                <table class='table table-striped'>
                    <thead>
                        <tr>
                            <th>Товар</th>
                            <th>Количество</th>
                        </tr>
                    </thead>
                    <tbody>
                        {string.Join("", supply.SupplyItems.Select(si => $@"
                            <tr>
                                <td>{si.Product?.Name ?? "-"}</td>
                                <td>{si.Quantity} шт.</td>
                            </tr>
                        "))}
                    </tbody>
                </table>";
        }

        var html = $@"
            <div class='mb-3'>
                <strong>Дата:</strong> {supplyDateDisplay:dd.MM.yyyy HH:mm}
            </div>
            <div class='mb-3'>
                <strong>Поставщик:</strong> {(string.IsNullOrWhiteSpace(supply.SupplierName) ? "-" : supply.SupplierName)}
            </div>
            <div class='mb-3'>
                <strong>Принял:</strong> {supply.ReceivedByUser?.FullName ?? "-"}
            </div>
            {(string.IsNullOrWhiteSpace(supply.Notes) ? "" : $@"
            <div class='mb-3'>
                <strong>Примечания:</strong> {supply.Notes}
            </div>")}
            {tableContent}
        ";

        return Content(html, "text/html");
    }

    // Методы для загрузки отчётов
    private async Task LoadReportDataAsync()
    {
        ReportData = new ReportDataModel();
        
        switch (ReportType?.ToLower())
        {
            case "sales":
                await LoadSalesReportAsync();
                break;
            case "services":
                await LoadServicesReportAsync();
                break;
            case "products":
                await LoadProductsReportAsync();
                break;
            case "financial":
                await LoadFinancialReportAsync();
                break;
        }
    }

    private async Task LoadSalesReportAsync()
    {
        var salesQuery = _context.Sales
            .Include(s => s.Seller)
            .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
            .AsQueryable();

        if (FilterDateFrom.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.SaleDate >= FilterDateFrom.Value);
        }
        if (FilterDateTo.HasValue)
        {
            var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
            salesQuery = salesQuery.Where(s => s.SaleDate <= dateTo);
        }
        if (FilterSellerId.HasValue && FilterSellerId.Value > 0)
        {
            salesQuery = salesQuery.Where(s => s.SellerId == FilterSellerId.Value);
        }

        var sales = await salesQuery.OrderByDescending(s => s.SaleDate).ToListAsync();

        if (ReportData != null)
        {
            ReportData.SalesReport = new SalesReportData
            {
                TotalRevenue = sales.Sum(s => s.TotalAmount),
                TotalSales = sales.Count,
                AverageSaleAmount = sales.Any() ? sales.Average(s => s.TotalAmount) : 0,
                SalesBySeller = sales
                    .GroupBy(s => s.Seller)
                    .Select(g => new SalesBySellerData
                    {
                        SellerName = g.Key?.FullName ?? "Неизвестно",
                        TotalSales = g.Count(),
                        TotalRevenue = g.Sum(s => s.TotalAmount)
                    })
                    .OrderByDescending(x => x.TotalRevenue)
                    .ToList(),
                DailySales = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new DailySalesData
                    {
                        Date = g.Key,
                        SalesCount = g.Count(),
                        Revenue = g.Sum(s => s.TotalAmount)
                    })
                    .OrderBy(x => x.Date)
                    .ToList()
            };
        }
    }

    private async Task LoadServicesReportAsync()
    {
        var ordersQuery = _context.ServiceOrders
            .Include(so => so.Master)
            .Include(so => so.ServiceOrderItems)
                .ThenInclude(soi => soi.Service)
            .AsQueryable();

        if (FilterDateFrom.HasValue)
        {
            ordersQuery = ordersQuery.Where(so => so.OrderDate >= FilterDateFrom.Value);
        }
        if (FilterDateTo.HasValue)
        {
            var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
            ordersQuery = ordersQuery.Where(so => so.OrderDate <= dateTo);
        }
        if (FilterMasterId.HasValue && FilterMasterId.Value > 0)
        {
            ordersQuery = ordersQuery.Where(so => so.MasterId == FilterMasterId.Value);
        }

        var orders = await ordersQuery.OrderByDescending(so => so.OrderDate).ToListAsync();

        if (ReportData != null)
        {
            ReportData.ServicesReport = new ServicesReportData
            {
                TotalRevenue = orders.Sum(so => so.TotalAmount ?? 0),
                TotalOrders = orders.Count,
                AverageOrderAmount = orders.Any() ? orders.Average(so => so.TotalAmount ?? 0) : 0,
                ServicesByMaster = orders
                    .GroupBy(so => so.Master)
                    .Select(g => new ServicesByMasterData
                    {
                        MasterName = g.Key?.FullName ?? "Неизвестно",
                        TotalOrders = g.Count(),
                        TotalRevenue = g.Sum(so => so.TotalAmount ?? 0)
                    })
                    .OrderByDescending(x => x.TotalRevenue)
                    .ToList()
            };
        }
    }

    private async Task LoadProductsReportAsync()
    {
        var products = await _context.Products.ToListAsync();
        var salesItems = await _context.SaleItems
            .Include(si => si.Sale)
            .Include(si => si.Product)
            .Where(si => (!FilterDateFrom.HasValue || si.Sale.SaleDate >= FilterDateFrom.Value) &&
                        (!FilterDateTo.HasValue || si.Sale.SaleDate <= FilterDateTo.Value.AddDays(1).AddSeconds(-1)))
            .ToListAsync();

        if (ReportData != null)
        {
            ReportData.ProductsReport = new ProductsReportData
            {
                TotalProducts = products.Count,
                ActiveProducts = products.Count(p => p.IsActive),
                LowStockProducts = products.Count(p => p.IsActive && p.StockQuantity < 5),
                TotalStockValue = products.Where(p => p.IsActive).Sum(p => p.StockQuantity * p.CostPrice),
                TopSellingProducts = salesItems
                    .GroupBy(si => si.Product)
                    .Select(g => new TopSellingProductData
                    {
                        ProductName = g.Key?.Name ?? "Неизвестно",
                        QuantitySold = g.Sum(si => si.Quantity),
                        Revenue = g.Sum(si => si.Quantity * si.PriceAtSale)
                    })
                    .OrderByDescending(x => x.QuantitySold)
                    .Take(10)
                    .ToList()
            };
        }
    }

    private async Task LoadFinancialReportAsync()
    {
        var salesQuery = _context.Sales.AsQueryable();
        var servicesQuery = _context.ServiceOrders.AsQueryable();
        var suppliesQuery = _context.Supplies.AsQueryable();

        if (FilterDateFrom.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.SaleDate >= FilterDateFrom.Value);
            servicesQuery = servicesQuery.Where(so => so.OrderDate >= FilterDateFrom.Value);
            suppliesQuery = suppliesQuery.Where(s => s.SupplyDate >= FilterDateFrom.Value);
        }
        if (FilterDateTo.HasValue)
        {
            var dateTo = FilterDateTo.Value.AddDays(1).AddSeconds(-1);
            salesQuery = salesQuery.Where(s => s.SaleDate <= dateTo);
            servicesQuery = servicesQuery.Where(so => so.OrderDate <= dateTo);
            suppliesQuery = suppliesQuery.Where(s => s.SupplyDate <= dateTo);
        }

        var sales = await salesQuery.ToListAsync();
        var services = await servicesQuery.ToListAsync();
        var supplies = await suppliesQuery.ToListAsync();

        var salesRevenue = sales.Sum(s => s.TotalAmount);
        var servicesRevenue = services.Sum(so => so.TotalAmount ?? 0);
        var totalCost = supplies.Sum(s => s.TotalCost);
        var totalRevenue = salesRevenue + servicesRevenue;
        var profit = totalRevenue - totalCost;

        if (ReportData != null)
        {
            ReportData.FinancialReport = new FinancialReportData
            {
                SalesRevenue = salesRevenue,
                ServicesRevenue = servicesRevenue,
                TotalRevenue = totalRevenue,
                TotalCost = totalCost,
                Profit = profit,
                ProfitMargin = totalRevenue > 0 ? (profit / totalRevenue * 100) : 0
            };
        }
    }

    // Классы данных для отчётов
    public class ReportDataModel
    {
        public SalesReportData? SalesReport { get; set; }
        public ServicesReportData? ServicesReport { get; set; }
        public ProductsReportData? ProductsReport { get; set; }
        public FinancialReportData? FinancialReport { get; set; }
    }

    public class SalesReportData
    {
        public decimal TotalRevenue { get; set; }
        public int TotalSales { get; set; }
        public decimal AverageSaleAmount { get; set; }
        public List<SalesBySellerData> SalesBySeller { get; set; } = new();
        public List<DailySalesData> DailySales { get; set; } = new();
    }

    public class SalesBySellerData
    {
        public string SellerName { get; set; } = string.Empty;
        public int TotalSales { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class DailySalesData
    {
        public DateTime Date { get; set; }
        public int SalesCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ServicesReportData
    {
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public decimal AverageOrderAmount { get; set; }
        public List<ServicesByMasterData> ServicesByMaster { get; set; } = new();
    }

    public class ServicesByMasterData
    {
        public string MasterName { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class ProductsReportData
    {
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int LowStockProducts { get; set; }
        public decimal TotalStockValue { get; set; }
        public List<TopSellingProductData> TopSellingProducts { get; set; } = new();
    }

    public class TopSellingProductData
    {
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class FinancialReportData
    {
        public decimal SalesRevenue { get; set; }
        public decimal ServicesRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitMargin { get; set; }
    }
}

