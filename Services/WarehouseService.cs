using Microsoft.EntityFrameworkCore;
using VatTuPro.Data;
using VatTuPro.Models;
namespace VatTuPro.Services;

public class WarehouseService(AppDbContext db)
{
    // ── Change Notification ───────────────────────────────────────────────────
    /// <summary>MainLayout subscribe vào đây để tự refresh thông báo khi dữ liệu thay đổi.</summary>
    public event Func<Task>? OnDataChanged;

    private void NotifyDataChanged()
    {
        if (OnDataChanged is not null)
            // Fire-and-forget: không block caller, không conflict DbContext
            _ = Task.Run(async () =>
            {
                try { await OnDataChanged.Invoke(); }
                catch { /* bỏ qua lỗi từ UI layer */ }
            });
    }
    // ── Materials ────────────────────────────────────────────────────────────
    public async Task<List<Material>> GetMaterialsAsync() =>
        await db.Materials.AsNoTracking().OrderBy(m => m.Code).ToListAsync();

    public async Task<Material?> GetMaterialAsync(int id) =>
        await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);

    public async Task AddMaterialAsync(Material m)
    {
        db.Materials.Add(m);
        await db.SaveChangesAsync();
        await LogAsync("Thêm vật tư", $"{m.Code} - {m.Name}", "add");
        NotifyDataChanged();
    }

    public async Task UpdateMaterialAsync(Material m)
    {
        var tracked = db.ChangeTracker.Entries<Material>().FirstOrDefault(e => e.Entity.Id == m.Id);
        if (tracked != null)
        {
            tracked.State = EntityState.Detached;
        }
        db.Materials.Update(m);

        // Đồng bộ toàn bộ thông tin mới sang các ô kho đang chứa vật tư này (hoặc dọn trống ô nếu tồn kho = 0)
        var cells = await db.StorageCells.Where(c => c.MaterialId == m.Id || c.MaterialCode == m.Code).ToListAsync();
        foreach (var cell in cells)
        {
            if (m.Quantity <= 0)
            {
                ClearCell(cell);
            }
            else
            {
                AssignMaterialToCell(cell, m);
            }
        }

        await db.SaveChangesAsync();
        await LogAsync("Cập nhật vật tư", $"{m.Code} - {m.Name} → {m.Quantity} {m.Unit}", "edit");
        NotifyDataChanged();
    }

    public async Task AssignMaterialToCellAsync(int materialId, int cellId, string locationStr)
    {
        var mat = await db.Materials.FindAsync(materialId);
        if (mat != null)
        {
            if (!string.IsNullOrEmpty(locationStr))
                mat.Location = locationStr;

            // Xóa gán ô cũ trước để tránh tình trạng 1 vật tư nằm ở nhiều ô
            var oldCells = await db.StorageCells.Where(c => c.MaterialId == materialId || c.MaterialCode == mat.Code).ToListAsync();
            foreach (var c in oldCells)
            {
                if (cellId <= 0 || c.Id != cellId)
                {
                    ClearCell(c);
                }
            }

            if (cellId > 0)
            {
                var cell = await db.StorageCells.FindAsync(cellId);
                if (cell != null)
                {
                    AssignMaterialToCell(cell, mat);
                }
            }

            await db.SaveChangesAsync();
            NotifyDataChanged();
        }
    }

    public async Task DeleteMaterialAsync(int id)
    {
        var m = await db.Materials.FindAsync(id);
        if (m != null)
        {
            // Clear hoàn toàn ô kho chứa vật tư này trước khi xóa vật tư
            var cells = await db.StorageCells.Where(c => c.MaterialId == id || c.MaterialCode == m.Code).ToListAsync();
            foreach (var cell in cells)
            {
                ClearCell(cell);
            }

            db.Materials.Remove(m);
            await db.SaveChangesAsync();
            await LogAsync("Xóa vật tư", $"{m.Code} - {m.Name}", "delete");
            NotifyDataChanged();
        }
    }

    // Mặc định: User (khách xem, không cần đăng nhập)
    public bool IsLoggedIn { get; set; } = false;
    public UserRole CurrentRole { get; set; } = UserRole.User;
    public string CurrentUser { get; set; } = "Khách";

    public bool IsAdmin => CurrentRole == UserRole.ThuKho;

    public bool Login(string username, string password)
    {
        if (password != "AITS1605") return false;
        IsLoggedIn = true;
        CurrentRole = UserRole.ThuKho;
        CurrentUser = "Thủ Kho";
        return true;
    }

    public void Logout()
    {
        IsLoggedIn = false;
        CurrentRole = UserRole.User;
        CurrentUser = "Khách";
    }

    // ── Notifications & Alerts ─────────────────────────────────────────────
    public async Task<List<AlertNotification>> GetNotificationsAsync()
    {
        var list = new List<AlertNotification>();
        var materials = await db.Materials.AsNoTracking().ToListAsync();
        var pendingTxns = await db.Transactions.AsNoTracking().Where(t => t.Status == "Chờ duyệt").ToListAsync();

        foreach (var m in materials.Where(m => m.Quantity <= m.MinQuantity))
        {
            list.Add(new AlertNotification
            {
                Title = "⚠️ Cảnh báo tồn kho",
                Message = $"Vật tư '{m.Code} - {m.Name}' còn {m.Quantity} {m.Unit} (Dưới ngưỡng {m.MinQuantity})",
                Type = m.Quantity == 0 ? "danger" : "warning",
                LinkUrl = "/vattu"
            });
        }

        foreach (var m in materials.Where(m => m.IsExpiringSoon))
        {
            list.Add(new AlertNotification
            {
                Title = "⏳ Cảnh báo hạn sử dụng",
                Message = $"Lô {m.BatchNumber} vật tư '{m.Name}' hết hạn vào {m.ExpiryDate:dd/MM/yyyy}",
                Type = "danger",
                LinkUrl = "/vattu"
            });
        }

        if (pendingTxns.Any())
        {
            list.Add(new AlertNotification
            {
                Title = "📋 Phiếu chờ duyệt",
                Message = $"Có {pendingTxns.Count} phiếu giao dịch đang chờ duyệt",
                Type = "info",
                LinkUrl = "/giaodich"
            });
        }

        return list;
    }

    // ── Transactions ────────────────────────────────────────────────────────
    public async Task<List<Transaction>> GetTransactionsAsync() =>
        await db.Transactions.AsNoTracking().Include(t => t.Items).OrderByDescending(t => t.Date).ToListAsync();

    public async Task<Transaction?> GetTransactionAsync(int id) =>
        await db.Transactions.AsNoTracking().Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);

    public async Task AddTransactionAsync(Transaction t)
    {
        var now = DateTime.Now;
        var prefix = t.Type == "Nhập kho" ? "NK" : (t.Type == "Xuất kho" ? "XK" : "CK");
        t.TransactionId = $"{prefix}-{now:yyyyMMdd}-{new Random().Next(100, 999)}";
        t.Date = now;
        t.Status = "Chờ duyệt";
        if (string.IsNullOrEmpty(t.User)) t.User = CurrentUser;
        db.Transactions.Add(t);
        await db.SaveChangesAsync();
        await LogAsync($"Tạo phiếu {t.Type.ToLower()}",
            $"{t.TransactionId} ({t.ItemCount} loại VT - {t.TotalValue:N0} ₫)",
            t.Type == "Nhập kho" ? "add" : (t.Type == "Xuất kho" ? "remove" : "edit"));
        NotifyDataChanged();
    }

    private static void ClearCell(StorageCell c)
    {
        c.MaterialId = null; c.MaterialCode = ""; c.MaterialName = "";
        c.MaterialUnit = ""; c.MaterialQty = 0; c.MaterialBatch = "";
        c.MaterialExpiry = null; c.MaterialPrice = 0;
    }

    private static void AssignMaterialToCell(StorageCell c, Material m)
    {
        c.MaterialId = m.Id;
        c.MaterialCode = m.Code;
        c.MaterialName = m.Name;
        c.MaterialUnit = m.Unit;
        c.MaterialQty = m.Quantity;
        c.MaterialMinQty = m.MinQuantity;
        c.MaterialBatch = m.BatchNumber;
        c.MaterialExpiry = m.ExpiryDate;
        c.MaterialPrice = m.Price;
    }

    private async Task AssignMaterialToLocationCellsAsync(Material material, string locationStr)
    {
        if (string.IsNullOrWhiteSpace(locationStr)) return;

        var parts = locationStr.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        string zoneCode = parts.Length > 0 ? parts[0].Replace("Khu", "").Trim() : "";
        string rackCode = parts.Length > 1 ? parts[1].Trim() : "";
        string shelfLevel = parts.Length > 2 ? parts[2].Trim() : "";
        string cellPos = parts.Length > 3 ? parts[3].Trim() : "";

        var zones = await db.StorageZones
            .Include(z => z.Racks)
                .ThenInclude(r => r.Shelves)
                    .ThenInclude(s => s.Cells)
            .ToListAsync();

        var targetZone = zones.FirstOrDefault(z => string.Equals(z.Code, zoneCode, StringComparison.OrdinalIgnoreCase));
        if (targetZone == null) return;

        IEnumerable<StorageCell> candidates = targetZone.Racks.SelectMany(r => r.Shelves).SelectMany(s => s.Cells);

        if (!string.IsNullOrEmpty(rackCode))
        {
            var targetRack = targetZone.Racks.FirstOrDefault(r => string.Equals(r.Code, rackCode, StringComparison.OrdinalIgnoreCase));
            if (targetRack != null)
            {
                candidates = targetRack.Shelves.SelectMany(s => s.Cells);

                if (!string.IsNullOrEmpty(shelfLevel))
                {
                    var targetShelf = targetRack.Shelves.FirstOrDefault(s => string.Equals(s.Level, shelfLevel, StringComparison.OrdinalIgnoreCase));
                    if (targetShelf != null)
                    {
                        candidates = targetShelf.Cells;

                        if (!string.IsNullOrEmpty(cellPos))
                        {
                            candidates = targetShelf.Cells.Where(c => c.Position == cellPos);
                        }
                    }
                }
            }
        }

        var candidateList = candidates.ToList();
        if (!candidateList.Any()) return;

        // Ưu tiên chọn ô chưa có vật tư (MaterialId null) hoặc ô trùng MaterialId. Nếu tất cả đều đầy thì chọn ô đầu tiên.
        var chosenCell = candidateList.FirstOrDefault(c => !c.MaterialId.HasValue || c.MaterialId == material.Id)
                        ?? candidateList.FirstOrDefault();

        if (chosenCell != null)
        {
            var dbCell = await db.StorageCells.FindAsync(chosenCell.Id);
            if (dbCell != null)
            {
                AssignMaterialToCell(dbCell, material);
            }
        }
    }

    public async Task ApproveTransactionAsync(int id)
    {
        if (!IsAdmin) return; // Chỉ Thủ kho / Admin mới được quyền duyệt phiếu

        var t = await db.Transactions.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (t != null && t.Status != "Đã duyệt")
        {
            t.Status = "Đã duyệt";

            foreach (var item in t.Items)
            {
                if (item.MaterialId > 0 && item.Quantity > 0)
                {
                    var material = await db.Materials.FindAsync(item.MaterialId);
                    if (material != null)
                    {
                        if (t.Type == "Nhập kho")
                        {
                            material.Quantity += item.Quantity;
                        }
                        else if (t.Type == "Xuất kho")
                        {
                            material.Quantity = Math.Max(0, material.Quantity - item.Quantity);
                        }

                        // Cập nhật chuỗi Vị trí hiển thị
                        if (!string.IsNullOrEmpty(item.TargetCellLocation))
                        {
                            material.Location = item.TargetCellLocation;
                        }

                        // 1. Xóa liên kết ô nguồn CŨ khi Chuyển kho (thực hiện TRƯỚC KHI gán ô mới)
                        if (t.Type == "Chuyển kho")
                        {
                            if (item.SourceCellId.HasValue && item.SourceCellId.Value > 0)
                            {
                                var srcCell = await db.StorageCells.FindAsync(item.SourceCellId.Value);
                                if (srcCell != null && srcCell.MaterialId == material.Id && srcCell.Id != item.TargetCellId)
                                {
                                    ClearCell(srcCell);
                                }
                            }
                            else
                            {
                                var oldCells = await db.StorageCells.Where(c => c.MaterialId == material.Id).ToListAsync();
                                foreach (var c in oldCells)
                                {
                                    if (!item.TargetCellId.HasValue || c.Id != item.TargetCellId.Value)
                                    {
                                        ClearCell(c);
                                    }
                                }
                            }
                        }

                        // 2. Gán vật tư vào ô MỚI (chỉ dành cho Nhập kho & Chuyển kho)
                        if (t.Type == "Nhập kho" || t.Type == "Chuyển kho")
                        {
                            if (item.TargetCellId.HasValue && item.TargetCellId.Value > 0)
                            {
                                var cell = await db.StorageCells.FindAsync(item.TargetCellId.Value);
                                if (cell != null)
                                {
                                    AssignMaterialToCell(cell, material);
                                }
                            }
                            else if (!string.IsNullOrEmpty(item.TargetCellLocation))
                            {
                                // Chuyển hoặc nhập vào Khu/Kệ/Tầng không chọn ô lẻ: Tự động gán ô phù hợp ở vị trí đích
                                await AssignMaterialToLocationCellsAsync(material, item.TargetCellLocation);
                            }
                        }

                        // 3. Đồng bộ số lượng trên các ô chứa vật tư và giải phóng nếu Quantity == 0
                        var existingCells = await db.StorageCells.Where(c => c.MaterialId == material.Id).ToListAsync();
                        foreach (var c in existingCells)
                        {
                            c.MaterialQty = material.Quantity;
                            if (material.Quantity == 0)
                            {
                                ClearCell(c);
                            }
                        }

                        await LogAsync(
                            t.Type == "Nhập kho" ? "Cộng tồn kho" : (t.Type == "Xuất kho" ? "Trừ tồn kho" : "Chuyển kho"),
                            $"{material.Code} - {material.Name}: Phiếu {t.TransactionId} ({t.Warehouse}) - Vị trí: {material.Location}",
                            "edit"
                        );
                    }
                }
            }

            await db.SaveChangesAsync();
            await LogAsync("Duyệt phiếu", $"{t.TransactionId} ({t.Type})", "edit");
            NotifyDataChanged();
        }
    }

    public async Task DeleteTransactionAsync(int id)
    {
        var t = await db.Transactions.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (t != null)
        {
            db.TransactionItems.RemoveRange(t.Items);
            db.Transactions.Remove(t);
            await db.SaveChangesAsync();
            await LogAsync("Xóa phiếu", $"{t.TransactionId} ({t.Type})", "delete");
            NotifyDataChanged();
        }
    }

    // ── Suppliers ───────────────────────────────────────────────────────────
    public async Task<List<SupplierModel>> GetSuppliersAsync() =>
        await db.Suppliers.AsNoTracking().OrderBy(s => s.SupplierId).ToListAsync();

    public async Task AddSupplierAsync(SupplierModel s)
    {
        var count = await db.Suppliers.CountAsync() + 1;
        s.SupplierId = $"NCC-{count:D3}";
        db.Suppliers.Add(s);
        await db.SaveChangesAsync();
        await LogAsync("Thêm nhà cung cấp", $"{s.SupplierId} - {s.Name}", "add");
        NotifyDataChanged();
    }

    public async Task UpdateSupplierAsync(SupplierModel s)
    {
        var tracked = db.ChangeTracker.Entries<SupplierModel>().FirstOrDefault(e => e.Entity.Id == s.Id);
        if (tracked != null)
        {
            tracked.State = EntityState.Detached;
        }
        db.Suppliers.Update(s);
        await db.SaveChangesAsync();
        await LogAsync("Cập nhật nhà cung cấp", $"{s.SupplierId} - {s.Name}", "edit");
        NotifyDataChanged();
    }

    public async Task DeleteSupplierAsync(int id)
    {
        var s = await db.Suppliers.FindAsync(id);
        if (s != null)
        {
            db.Suppliers.Remove(s);
            await db.SaveChangesAsync();
            await LogAsync("Xóa nhà cung cấp", $"{s.SupplierId} - {s.Name}", "delete");
            NotifyDataChanged();
        }
    }

    // ── Storage Zones ────────────────────────────────────────────────────────
    public async Task<List<StorageZone>> GetZonesAsync() =>
        await db.StorageZones
            .AsNoTracking()
            .Include(z => z.Racks)
                .ThenInclude(r => r.Shelves)
                    .ThenInclude(s => s.Cells)
            .OrderBy(z => z.Code)
            .ToListAsync();

    public async Task AddZoneAsync(StorageZone zone)
    {
        db.StorageZones.Add(zone);
        await db.SaveChangesAsync();
        await LogAsync("Thêm khu vực", $"Khu {zone.Code} - {zone.Name}", "add");
        NotifyDataChanged();
    }

    public async Task UpdateZoneAsync(StorageZone zone)
    {
        var existing = await db.StorageZones.FindAsync(zone.Id);
        if (existing == null) return;
        existing.Code = zone.Code; existing.Name = zone.Name;
        existing.Color = zone.Color; existing.Description = zone.Description;
        await db.SaveChangesAsync();
        await LogAsync("Cập nhật khu vực", $"Khu {zone.Code} - {zone.Name}", "edit");
        NotifyDataChanged();
    }

    public async Task DeleteZoneAsync(int id)
    {
        var zone = await db.StorageZones
            .Include(z => z.Racks).ThenInclude(r => r.Shelves).ThenInclude(s => s.Cells)
            .FirstOrDefaultAsync(z => z.Id == id);
        if (zone == null) return;
        foreach (var r in zone.Racks)
        {
            foreach (var s in r.Shelves) db.StorageCells.RemoveRange(s.Cells);
            db.StorageShelves.RemoveRange(r.Shelves);
        }
        db.StorageRacks.RemoveRange(zone.Racks);
        db.StorageZones.Remove(zone);
        await db.SaveChangesAsync();
        await LogAsync("Xóa khu vực", $"Khu {zone.Code} - {zone.Name}", "delete");
        NotifyDataChanged();
    }

    // ── Storage Racks ────────────────────────────────────────────────────────
    public async Task AddRackAsync(StorageRack rack)
    {
        db.StorageRacks.Add(rack);
        await db.SaveChangesAsync();
        await LogAsync("Thêm kệ", $"{rack.Code} - {rack.Name}", "add");
        NotifyDataChanged();
    }

    public async Task UpdateRackAsync(StorageRack rack)
    {
        var existing = await db.StorageRacks.FindAsync(rack.Id);
        if (existing == null) return;
        existing.Code = rack.Code; existing.Name = rack.Name; existing.Description = rack.Description;
        await db.SaveChangesAsync();
        await LogAsync("Cập nhật kệ", $"{rack.Code} - {rack.Name}", "edit");
        NotifyDataChanged();
    }

    public async Task DeleteRackAsync(int id)
    {
        var rack = await db.StorageRacks
            .Include(r => r.Shelves).ThenInclude(s => s.Cells)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (rack == null) return;
        foreach (var s in rack.Shelves) db.StorageCells.RemoveRange(s.Cells);
        db.StorageShelves.RemoveRange(rack.Shelves);
        db.StorageRacks.Remove(rack);
        await db.SaveChangesAsync();
        await LogAsync("Xóa kệ", $"{rack.Code} - {rack.Name}", "delete");
        NotifyDataChanged();
    }

    // ── Storage Shelves ──────────────────────────────────────────────────────
    public async Task AddShelfAsync(StorageShelf shelf)
    {
        // Auto sort order = max + 1
        var maxOrder = await db.StorageShelves.Where(s => s.RackId == shelf.RackId).MaxAsync(s => (int?)s.SortOrder) ?? 0;
        shelf.SortOrder = maxOrder + 1;
        db.StorageShelves.Add(shelf);
        await db.SaveChangesAsync();
        await LogAsync("Thêm tầng", $"Tầng {shelf.Level}", "add");
        NotifyDataChanged();
    }

    public async Task UpdateShelfAsync(StorageShelf shelf)
    {
        var existing = await db.StorageShelves.FindAsync(shelf.Id);
        if (existing == null) return;
        existing.Level = shelf.Level;
        await db.SaveChangesAsync();
        await LogAsync("Cập nhật tầng", $"Tầng {shelf.Level}", "edit");
        NotifyDataChanged();
    }

    public async Task DeleteShelfAsync(int id)
    {
        var shelf = await db.StorageShelves.Include(s => s.Cells).FirstOrDefaultAsync(s => s.Id == id);
        if (shelf == null) return;
        db.StorageCells.RemoveRange(shelf.Cells);
        db.StorageShelves.Remove(shelf);
        await db.SaveChangesAsync();
        await LogAsync("Xóa tầng", $"Tầng {shelf.Level}", "delete");
        NotifyDataChanged();
    }

    // ── Storage Cells ────────────────────────────────────────────────────────
    public async Task AddCellAsync(StorageCell cell)
    {
        db.StorageCells.Add(cell);
        await db.SaveChangesAsync();
        await LogAsync("Thêm ô", $"Ô {cell.Position}", "add");
        NotifyDataChanged();
    }

    public async Task UpdateCellAsync(StorageCell cell)
    {
        var tracked = db.ChangeTracker.Entries<StorageCell>().FirstOrDefault(e => e.Entity.Id == cell.Id);
        if (tracked != null) tracked.State = EntityState.Detached;
        db.StorageCells.Update(cell);
        await db.SaveChangesAsync();
        await LogAsync("Cập nhật ô", $"Ô {cell.Position} - {cell.MaterialName}", "edit");
        NotifyDataChanged();
    }

    public async Task DeleteCellAsync(int id)
    {
        var cell = await db.StorageCells.FindAsync(id);
        if (cell == null) return;
        db.StorageCells.Remove(cell);
        await db.SaveChangesAsync();
        await LogAsync("Xóa ô", $"Ô {cell.Position}", "delete");
        NotifyDataChanged();
    }

    public async Task AssignMaterialToCellAsync(int cellId, Material? mat)
    {
        var cell = await db.StorageCells.FindAsync(cellId);
        if (cell == null) return;
        if (mat == null)
        {
            // Clear cell
            cell.MaterialId = null; cell.MaterialCode = ""; cell.MaterialName = "";
            cell.MaterialUnit = ""; cell.MaterialQty = 0; cell.MaterialMinQty = 0;
            cell.MaterialBatch = ""; cell.MaterialExpiry = null; cell.MaterialPrice = 0;
            await LogAsync("Dọn trống ô", $"Ô {cell.Position}", "edit");
        }
        else
        {
            cell.MaterialId = mat.Id; cell.MaterialCode = mat.Code; cell.MaterialName = mat.Name;
            cell.MaterialUnit = mat.Unit; cell.MaterialQty = mat.Quantity; cell.MaterialMinQty = mat.MinQuantity;
            cell.MaterialBatch = mat.BatchNumber; cell.MaterialExpiry = mat.ExpiryDate; cell.MaterialPrice = mat.Price;
            await LogAsync("Gán vật tư vào ô", $"{mat.Code} - {mat.Name} → Ô {cell.Position}", "edit");
        }
        await db.SaveChangesAsync();
        NotifyDataChanged();
    }

    public async Task<List<CellLocationItem>> GetAllCellLocationsAsync()
    {
        var zones = await GetZonesAsync();
        var list = new List<CellLocationItem>();
        foreach (var z in zones)
        {
            foreach (var r in (z.Racks ?? []))
            {
                foreach (var s in (r.Shelves ?? []))
                {
                    foreach (var c in (s.Cells ?? []))
                    {
                        var locStr = $"{z.Code} > {r.Code} > {s.Level} > {c.Position}";
                        var status = c.IsOccupied ? $" (Chứa: {c.MaterialName})" : " (Trống)";
                        list.Add(new CellLocationItem
                        {
                            CellId = c.Id,
                            LocationString = locStr,
                            DisplayText = $"{locStr}{status}",
                            MaterialId = c.MaterialId
                        });
                    }
                }
            }
        }
        return list;
    }


    // ── Audit Log ───────────────────────────────────────────────────────────
    public async Task<List<AuditLog>> GetAuditLogsAsync(int take = 100) =>
        await db.AuditLogs.AsNoTracking().OrderByDescending(a => a.Time).Take(take).ToListAsync();

    public async Task LogAsync(string action, string item, string type, string? user = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Time = DateTime.Now,
            Action = action,
            Item = item,
            User = user ?? CurrentUser,
            Type = type,
        });
        await db.SaveChangesAsync();
    }

    // ── Dashboard Stats ─────────────────────────────────────────────────────
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var materials = await db.Materials.AsNoTracking().ToListAsync();
        var txns = await db.Transactions.AsNoTracking().ToListAsync();
        var today = DateTime.Today;
        return new DashboardStats
        {
            TotalMaterials = materials.Count,
            LowStockCount = materials.Count(m => m.Status != "Bình thường"),
            TodayImport = txns.Count(t => t.Type == "Nhập kho" && t.Date.Date == today),
            TodayExport = txns.Count(t => t.Type == "Xuất kho" && t.Date.Date == today),
            TotalImportValue = txns.Where(t => t.Type == "Nhập kho").Sum(t => t.TotalValue),
            TotalExportValue = txns.Where(t => t.Type == "Xuất kho").Sum(t => t.TotalValue),
            StockValue = materials.Sum(m => m.Quantity * m.Price),
            LowStockItems = materials.Where(m => m.Status != "Bình thường").Take(5).ToList(),
            RecentTransactions = txns.OrderByDescending(t => t.Date).Take(5).ToList(),
        };
    }

    public async Task<List<Bin3DModel>> Get3DWarehouseDataAsync()
    {
        var result = new List<Bin3DModel>();
        var zones = await db.StorageZones
            .AsNoTracking()
            .Include(z => z.Racks)
                .ThenInclude(r => r.Shelves)
                    .ThenInclude(s => s.Cells)
            .OrderBy(z => z.Code)
            .ToListAsync();

        var materials = await db.Materials.AsNoTracking().ToListAsync();

        int zoneIdx = 0;
        foreach (var z in zones)
        {
            int globalBayIdx = 0;

            if (z.Racks != null && z.Racks.Any())
            {
                foreach (var r in z.Racks)
                {
                    if (r.Shelves != null && r.Shelves.Any())
                    {
                        int maxCellsInRack = r.Shelves.Max(s => s.Cells?.Count ?? 0);
                        if (maxCellsInRack == 0) maxCellsInRack = 1;

                        foreach (var s in r.Shelves.OrderBy(s => s.SortOrder))
                        {
                            int cellPosIdx = 0;
                            if (s.Cells != null && s.Cells.Any())
                            {
                                foreach (var c in s.Cells)
                                {
                                    var locStr = $"{z.Code} > {r.Code} > {s.Level} > {c.Position}";
                                    var matchingMats = materials.Where(m =>
                                        m.Quantity > 0 && (
                                            m.Location == locStr ||
                                            (c.MaterialId.HasValue && m.Id == c.MaterialId.Value) ||
                                            (!string.IsNullOrEmpty(c.MaterialCode) && m.Code == c.MaterialCode)
                                        )
                                    ).DistinctBy(m => m.Id).ToList();

                                    var binMatItems = matchingMats.Select(m => new BinMaterialItem
                                    {
                                        MaterialId = m.Id,
                                        MaterialCode = m.Code,
                                        MaterialName = m.Name,
                                        Unit = m.Unit,
                                        Quantity = m.Quantity,
                                        MinQuantity = m.MinQuantity,
                                        BatchNumber = m.BatchNumber,
                                        ExpiryDate = m.ExpiryDate,
                                        Price = m.Price
                                    }).ToList();

                                    if (!binMatItems.Any() && c.MaterialQty > 0 && !string.IsNullOrEmpty(c.MaterialName))
                                    {
                                        binMatItems.Add(new BinMaterialItem
                                        {
                                            MaterialId = c.MaterialId ?? 0,
                                            MaterialCode = c.MaterialCode ?? "",
                                            MaterialName = c.MaterialName ?? "",
                                            Unit = c.MaterialUnit ?? "",
                                            Quantity = c.MaterialQty,
                                            MinQuantity = c.MaterialMinQty,
                                            BatchNumber = c.MaterialBatch ?? "",
                                            ExpiryDate = c.MaterialExpiry,
                                            Price = c.MaterialPrice
                                        });
                                    }

                                    bool hasMaterial = binMatItems.Any();
                                    bool isExpiring = binMatItems.Any(m => m.IsExpiringSoon);
                                    string status = !hasMaterial ? "empty" : (isExpiring ? "expiring" : "occupied");
                                    var firstMat = binMatItems.FirstOrDefault();

                                    result.Add(new Bin3DModel
                                    {
                                        Id = $"{z.Code}-{r.Code}-S{s.Level}-C{c.Position}",
                                        CellId = c.Id,
                                        ZoneCode = z.Code,
                                        RackCode = r.Code,
                                        ShelfLevel = s.Level,
                                        Position = c.Position,
                                        Aisle = zoneIdx,
                                        Bay = globalBayIdx + cellPosIdx,
                                        Level = s.SortOrder > 0 ? s.SortOrder - 1 : 0,
                                        ZoneColor = string.IsNullOrEmpty(z.Color) ? "#3b82f6" : z.Color,
                                        IsExpiring = isExpiring,
                                        Status = status,
                                        MaterialId = firstMat?.MaterialId ?? 0,
                                        MaterialCode = firstMat?.MaterialCode ?? "",
                                        MaterialName = firstMat?.MaterialName ?? "",
                                        Unit = firstMat?.Unit ?? "",
                                        Quantity = firstMat?.Quantity ?? 0,
                                        MinQuantity = firstMat?.MinQuantity ?? 0,
                                        BatchNumber = firstMat?.BatchNumber ?? "",
                                        ExpiryDate = firstMat?.ExpiryDate,
                                        Price = firstMat?.Price ?? 0,
                                        Location = locStr,
                                        Materials = binMatItems
                                    });
                                    cellPosIdx++;
                                }
                            }
                            else
                            {
                                result.Add(new Bin3DModel
                                {
                                    Id = $"{z.Code}-{r.Code}-S{s.Level}-Rỗng",
                                    CellId = 0,
                                    ZoneCode = z.Code,
                                    RackCode = r.Code,
                                    ShelfLevel = s.Level,
                                    Position = "1",
                                    Aisle = zoneIdx,
                                    Bay = globalBayIdx,
                                    Level = s.SortOrder > 0 ? s.SortOrder - 1 : 0,
                                    ZoneColor = string.IsNullOrEmpty(z.Color) ? "#3b82f6" : z.Color,
                                    IsExpiring = false,
                                    Status = "empty",
                                    Location = $"{z.Code} > {r.Code} > {s.Level}"
                                });
                            }
                        }
                        globalBayIdx += maxCellsInRack + 1;
                    }
                    else
                    {
                        result.Add(new Bin3DModel
                        {
                            Id = $"{z.Code}-{r.Code}-Rỗng",
                            CellId = 0,
                            ZoneCode = z.Code,
                            RackCode = r.Code,
                            ShelfLevel = "I",
                            Position = "1",
                            Aisle = zoneIdx,
                            Bay = globalBayIdx,
                            Level = 0,
                            ZoneColor = string.IsNullOrEmpty(z.Color) ? "#3b82f6" : z.Color,
                            IsExpiring = false,
                            Status = "empty",
                            Location = $"{z.Code} > {r.Code}"
                        });
                        globalBayIdx += 2;
                    }
                }
            }
            else
            {
                result.Add(new Bin3DModel
                {
                    Id = $"Khu-{z.Code}-Rỗng",
                    CellId = 0,
                    ZoneCode = z.Code,
                    RackCode = "A1",
                    ShelfLevel = "I",
                    Position = "1",
                    Aisle = zoneIdx,
                    Bay = 0,
                    Level = 0,
                    ZoneColor = string.IsNullOrEmpty(z.Color) ? "#3b82f6" : z.Color,
                    IsExpiring = false,
                    Status = "empty",
                    Location = $"Khu {z.Code}"
                });
            }
            zoneIdx++;
        }

        return result;
    }
}

public class Bin3DModel
{
    public string Id { get; set; } = "";
    public int CellId { get; set; }
    public string ZoneCode { get; set; } = "";
    public string RackCode { get; set; } = "";
    public string ShelfLevel { get; set; } = "";
    public string Position { get; set; } = "";
    public int Aisle { get; set; }
    public int Bay { get; set; }
    public int Level { get; set; }
    public string ZoneColor { get; set; } = "#3b82f6";
    public bool IsExpiring { get; set; } = false;
    public string Status { get; set; } = "normal";
    public int MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Quantity { get; set; }
    public int MinQuantity { get; set; }
    public string BatchNumber { get; set; } = "";
    public DateTime? ExpiryDate { get; set; }
    public decimal Price { get; set; }
    public string Location { get; set; } = "";
    public List<BinMaterialItem> Materials { get; set; } = [];
}

public class BinMaterialItem
{
    public int MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Quantity { get; set; }
    public int MinQuantity { get; set; }
    public string BatchNumber { get; set; } = "";
    public DateTime? ExpiryDate { get; set; }
    public decimal Price { get; set; }
    public bool IsExpiringSoon => ExpiryDate.HasValue && ExpiryDate.Value <= DateTime.Now.AddDays(45);
}

public class DashboardStats
{
    public int TotalMaterials { get; set; }
    public int LowStockCount { get; set; }
    public int TodayImport { get; set; }
    public int TodayExport { get; set; }
    public decimal TotalImportValue { get; set; }
    public decimal TotalExportValue { get; set; }
    public decimal StockValue { get; set; }
    public List<Material> LowStockItems { get; set; } = [];
    public List<Transaction> RecentTransactions { get; set; } = [];
}

public class CellLocationItem
{
    public int CellId { get; set; }
    public string LocationString { get; set; } = "";
    public string DisplayText { get; set; } = "";
    public int? MaterialId { get; set; }
}
