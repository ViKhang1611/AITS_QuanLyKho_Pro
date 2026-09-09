using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VatTuPro.Models;

public class Material
{
    public int Id { get; set; }
    [Required] public string Code { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Quantity { get; set; }
    public int MinQuantity { get; set; }
    public string Location { get; set; } = "";
    public string Supplier { get; set; } = "";
    public decimal Price { get; set; }
    public string BatchNumber { get; set; } = "LÔ-2026-01";
    public DateTime? ExpiryDate { get; set; } = DateTime.Now.AddMonths(12);
    public string Barcode { get; set; } = "";

    public string Status => Quantity == 0 ? "Hết hàng"
        : Quantity < MinQuantity ? "Sắp hết"
        : "Bình thường";

    public bool IsExpiringSoon => ExpiryDate.HasValue && ExpiryDate.Value <= DateTime.Now.AddDays(45);
}

public class Transaction
{
    public int Id { get; set; }
    public string TransactionId { get; set; } = "";
    public string Type { get; set; } = "Nhập kho"; // Nhập kho | Xuất kho | Chuyển kho
    public DateTime Date { get; set; } = DateTime.Now;
    public int ItemCount => Items.Count;
    public decimal TotalValue => Items.Sum(i => i.TotalValue);
    public string Warehouse { get; set; } = "Kho A";
    public string FromWarehouse { get; set; } = "";
    public string ToWarehouse { get; set; } = "";
    public string User { get; set; } = "";
    public string Note { get; set; } = "";
    public string Status { get; set; } = "Chờ duyệt";
    public List<TransactionItem> Items { get; set; } = [];
}

public class TransactionItem
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public int MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string BatchNumber { get; set; } = "";
    public DateTime? ExpiryDate { get; set; }
    public string TargetCellLocation { get; set; } = ""; // Vị trí Kệ - Tầng - Ô đích (VD: A > A1 > I > 1)
    public int? TargetCellId { get; set; }                // ID ô chứa mục tiêu
    public string SourceCellLocation { get; set; } = ""; // Vị trí ô nguồn (cho phiếu chuyển kho)
    public int? SourceCellId { get; set; }               // ID ô nguồn (cho phiếu chuyển kho)

    [NotMapped] public int SelectedRackId { get; set; }
    [NotMapped] public int SelectedShelfId { get; set; }
    [NotMapped] public int SourceSelectedRackId { get; set; }
    [NotMapped] public int SourceSelectedShelfId { get; set; }

    public decimal TotalValue => Quantity * UnitPrice;
}

public class SupplierModel
{
    public int Id { get; set; }
    public string SupplierId { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string Category { get; set; } = "";
    public int Rating { get; set; } = 3;
    public int TotalOrders { get; set; }
    public string TotalValue { get; set; } = "0 ₫";
    public string Status { get; set; } = "Đang hợp tác";
    public string LastOrder { get; set; } = "-";
}

public class StorageZone
{
    public int Id { get; set; }
    public string Code { get; set; } = "";         // "A", "B", "C"
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#3b82f6";  // hex color for UI
    public string Description { get; set; } = "";
    public List<StorageRack> Racks { get; set; } = new();

    public int TotalCells => Racks?.Sum(r => r.Shelves?.Sum(s => s.Cells?.Count ?? 0) ?? 0) ?? 0;
    public int OccupiedCells => Racks?.Sum(r => r.Shelves?.Sum(s => s.Cells?.Count(c => c?.IsOccupied == true) ?? 0) ?? 0) ?? 0;
}

public class StorageRack
{
    public int Id { get; set; }
    public int ZoneId { get; set; }
    public string Code { get; set; } = "";     // "A1", "A2"
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<StorageShelf> Shelves { get; set; } = new();

    public int TotalCells => Shelves?.Sum(s => s.Cells?.Count ?? 0) ?? 0;
    public int OccupiedCells => Shelves?.Sum(s => s.Cells?.Count(c => c?.IsOccupied == true) ?? 0) ?? 0;
    public double OccupancyRate => TotalCells == 0 ? 0 : (double)OccupiedCells / TotalCells * 100;
}

public class StorageShelf
{
    public int Id { get; set; }
    public int RackId { get; set; }
    public string Level { get; set; } = "";    // "I", "II", "III"
    public int SortOrder { get; set; }         // 1, 2, 3... for ordering bottom→top
    public List<StorageCell> Cells { get; set; } = new();

    public int OccupiedCells => Cells?.Count(c => c?.IsOccupied == true) ?? 0;
}

public class StorageCell
{
    public int Id { get; set; }
    public int ShelfId { get; set; }
    public string Position { get; set; } = "";  // "1", "2", "3"
    public string Note { get; set; } = "";
    public int? MaterialId { get; set; }         // nullable FK to Material
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string MaterialUnit { get; set; } = "";
    public int MaterialQty { get; set; }
    public int MaterialMinQty { get; set; }
    public string MaterialBatch { get; set; } = "";
    public DateTime? MaterialExpiry { get; set; }
    public decimal MaterialPrice { get; set; }

    public bool IsOccupied => MaterialId.HasValue;
    public string StatusColor => !IsOccupied ? "empty"
        : MaterialExpiry.HasValue && MaterialExpiry.Value <= DateTime.Now.AddDays(45) ? "expiring"
        : MaterialQty < MaterialMinQty ? "low"
        : "ok";
}

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Time { get; set; } = DateTime.Now;
    public string Action { get; set; } = "";
    public string Item { get; set; } = "";
    public string User { get; set; } = "Admin";
    public string Type { get; set; } = "edit"; // add | edit | remove | delete
}

public enum UserRole
{
    User,    // Khách / Xem (mặc định, không cần đăng nhập)
    ThuKho   // Thủ Kho = Admin (có đầy đủ quyền quản lý)
}

public class AlertNotification
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Type { get; set; } = "warning"; // warning | info | danger
    public DateTime Time { get; set; } = DateTime.Now;
    public string LinkUrl { get; set; } = "";
}
