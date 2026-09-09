using Microsoft.EntityFrameworkCore;
using VatTuPro.Models;

namespace VatTuPro.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    public DbSet<SupplierModel> Suppliers => Set<SupplierModel>();
    public DbSet<StorageZone> StorageZones => Set<StorageZone>();
    public DbSet<StorageRack> StorageRacks => Set<StorageRack>();
    public DbSet<StorageShelf> StorageShelves => Set<StorageShelf>();
    public DbSet<StorageCell> StorageCells => Set<StorageCell>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Explicit FK mappings for storage hierarchy ───────────────────────
        modelBuilder.Entity<StorageRack>()
            .HasOne<StorageZone>()
            .WithMany(z => z.Racks)
            .HasForeignKey(r => r.ZoneId);

        modelBuilder.Entity<StorageShelf>()
            .HasOne<StorageRack>()
            .WithMany(r => r.Shelves)
            .HasForeignKey(s => s.RackId);

        modelBuilder.Entity<StorageCell>()
            .HasOne<StorageShelf>()
            .WithMany(s => s.Cells)
            .HasForeignKey(c => c.ShelfId);

        modelBuilder.Entity<Material>().HasData(
            new Material { Id=1, Code="VT-0001", Name="Xi măng Hà Tiên PCB40", Category="Vật liệu xây dựng", Unit="Bao 50kg", Quantity=1250, MinQuantity=500, Location="A > A1 > I > 1", Supplier="Hà Tiên 1", Price=95000, BatchNumber="LÔ-2026-01", ExpiryDate=new DateTime(2027,3,15), Barcode="89350010001" },
            new Material { Id=2, Code="VT-0002", Name="Sắt thép phi 12", Category="Vật liệu xây dựng", Unit="Thanh 11.7m", Quantity=320, MinQuantity=100, Location="A > A1 > I > 2", Supplier="Hòa Phát", Price=185000, BatchNumber="LÔ-2026-02", ExpiryDate=new DateTime(2028,1,10), Barcode="89350010002" },
            new Material { Id=3, Code="VT-0003", Name="Dây điện CVV 2.5mm", Category="Vật tư điện", Unit="Cuộn 100m", Quantity=12, MinQuantity=50, Location="B > B1 > I > 1", Supplier="CADIVI", Price=1250000, BatchNumber="LÔ-2026-03", ExpiryDate=new DateTime(2029,6,30), Barcode="89350010003" },
            new Material { Id=4, Code="VT-0004", Name="Ống nước PVC Ø90mm", Category="Vật tư cơ khí", Unit="Cây 4m", Quantity=560, MinQuantity=200, Location="A > A2 > I > 1", Supplier="Tiền Phong", Price=145000, BatchNumber="LÔ-2026-04", ExpiryDate=new DateTime(2030,12,31), Barcode="89350010004" },
            new Material { Id=5, Code="VT-0005", Name="Bu lông M12 x 50", Category="Phụ kiện", Unit="Hộp 100 cái", Quantity=8, MinQuantity=30, Location="B > B1 > II > 1", Supplier="Kim Long", Price=45000, BatchNumber="LÔ-2026-05", ExpiryDate=new DateTime(2028,5,20), Barcode="89350010005" },
            new Material { Id=6, Code="VT-0006", Name="Sơn nước Dulux trắng 18L", Category="Vật liệu hoàn thiện", Unit="Thùng", Quantity=87, MinQuantity=40, Location="C > C1 > I > 1", Supplier="AkzoNobel", Price=1350000, BatchNumber="LÔ-2026-06", ExpiryDate=new DateTime(2026,9,20), Barcode="89350010006" },
            new Material { Id=7, Code="VT-0007", Name="Gạch men 60x60 trắng bóng", Category="Vật liệu xây dựng", Unit="Thùng 4 viên", Quantity=243, MinQuantity=80, Location="A > A1 > II > 1", Supplier="Đồng Tâm", Price=235000, BatchNumber="LÔ-2026-07", ExpiryDate=new DateTime(2030,1,1), Barcode="89350010007" },
            new Material { Id=8, Code="VT-0008", Name="Dây điện CVV 1.5mm", Category="Vật tư điện", Unit="Cuộn 100m", Quantity=28, MinQuantity=40, Location="B > B2 > I > 1", Supplier="CADIVI", Price=780000, BatchNumber="LÔ-2026-08", ExpiryDate=new DateTime(2027,11,15), Barcode="89350010008" },
            new Material { Id=9, Code="VT-0009", Name="Ổ cắm điện 3 lỗ", Category="Vật tư điện", Unit="Cái", Quantity=8, MinQuantity=30, Location="B > B2 > II > 1", Supplier="Sino", Price=35000, BatchNumber="LÔ-2026-09", ExpiryDate=new DateTime(2028,8,8), Barcode="89350010009" },
            new Material { Id=10, Code="VT-0010", Name="Nhớt máy 10W-40", Category="Hóa chất", Unit="Lít", Quantity=20, MinQuantity=60, Location="C > C1 > II > 1", Supplier="Castrol", Price=75000, BatchNumber="LÔ-2026-10", ExpiryDate=new DateTime(2026,10,1), Barcode="89350010010" }
        );

        modelBuilder.Entity<Transaction>().HasData(
            new Transaction { Id=1, TransactionId="NK-20260313-001", Type="Nhập kho", Date=new DateTime(2026,3,13,9,15,0), Warehouse="Kho A", FromWarehouse="", ToWarehouse="Kho A", User="Nguyễn Văn A", Note="Nhập theo PO-2026-0142", Status="Đã duyệt" },
            new Transaction { Id=2, TransactionId="XK-20260313-001", Type="Xuất kho", Date=new DateTime(2026,3,13,10,30,0), Warehouse="Kho B", FromWarehouse="Kho B", ToWarehouse="", User="Trần Thị B", Note="Xuất cho dự án CT-2026-07", Status="Đã duyệt" },
            new Transaction { Id=3, TransactionId="CK-20260313-001", Type="Chuyển kho", Date=new DateTime(2026,3,13,11,45,0), Warehouse="Kho A -> Kho B", FromWarehouse="Kho A", ToWarehouse="Kho B", User="Lê Văn C", Note="Chuyển bổ sung Xi măng từ Kho A sang Kho B", Status="Chờ duyệt" },
            new Transaction { Id=4, TransactionId="XK-20260312-003", Type="Xuất kho", Date=new DateTime(2026,3,12,14,0,0), Warehouse="Kho C", FromWarehouse="Kho C", ToWarehouse="", User="Phạm Thị D", Note="Xuất bảo trì thiết bị", Status="Đã duyệt" },
            new Transaction { Id=5, TransactionId="NK-20260312-001", Type="Nhập kho", Date=new DateTime(2026,3,12,8,30,0), Warehouse="Kho A", FromWarehouse="", ToWarehouse="Kho A", User="Hoàng Văn E", Note="Nhập định kỳ tháng 3", Status="Đã duyệt" }
        );

        modelBuilder.Entity<TransactionItem>().HasData(
            new TransactionItem { Id=1, TransactionId=1, MaterialId=1, MaterialCode="VT-0001", MaterialName="Xi măng Hà Tiên PCB40", Unit="Bao 50kg", Quantity=100, UnitPrice=95000, BatchNumber="LÔ-2026-01" },
            new TransactionItem { Id=2, TransactionId=1, MaterialId=2, MaterialCode="VT-0002", MaterialName="Sắt thép phi 12", Unit="Thanh 11.7m", Quantity=50, UnitPrice=185000, BatchNumber="LÔ-2026-02" },
            new TransactionItem { Id=3, TransactionId=2, MaterialId=3, MaterialCode="VT-0003", MaterialName="Dây điện CVV 2.5mm", Unit="Cuộn 100m", Quantity=3, UnitPrice=1250000, BatchNumber="LÔ-2026-03" },
            new TransactionItem { Id=4, TransactionId=3, MaterialId=1, MaterialCode="VT-0001", MaterialName="Xi măng Hà Tiên PCB40", Unit="Bao 50kg", Quantity=200, UnitPrice=95000, BatchNumber="LÔ-2026-01" },
            new TransactionItem { Id=5, TransactionId=3, MaterialId=4, MaterialCode="VT-0004", MaterialName="Ống nước PVC Ø90mm", Unit="Cây 4m", Quantity=100, UnitPrice=145000, BatchNumber="LÔ-2026-04" },
            new TransactionItem { Id=6, TransactionId=4, MaterialId=10, MaterialCode="VT-0010", MaterialName="Nhớt máy 10W-40", Unit="Lít", Quantity=5, UnitPrice=75000, BatchNumber="LÔ-2026-10" },
            new TransactionItem { Id=7, TransactionId=5, MaterialId=2, MaterialCode="VT-0002", MaterialName="Sắt thép phi 12", Unit="Thanh 11.7m", Quantity=200, UnitPrice=185000, BatchNumber="LÔ-2026-02" }
        );

        modelBuilder.Entity<SupplierModel>().HasData(
            new SupplierModel { Id=1, SupplierId="NCC-001", Name="Công ty TNHH Hà Tiên 1", Contact="Nguyễn Minh Khoa", Phone="028 3825 1234", Email="sales@hatien1.vn", Address="Thủ Đức, TP.HCM", Category="VLXD", Rating=5, TotalOrders=48, TotalValue="1.2 tỷ ₫", Status="Đang hợp tác", LastOrder="10/03/2026" },
            new SupplierModel { Id=2, SupplierId="NCC-002", Name="Tập đoàn Hòa Phát", Contact="Trần Văn Hùng", Phone="024 3974 1818", Email="kinh.doanh@hoaphat.vn", Address="Đông Anh, Hà Nội", Category="Thép", Rating=4, TotalOrders=32, TotalValue="856M ₫", Status="Đang hợp tác", LastOrder="08/03/2026" },
            new SupplierModel { Id=3, SupplierId="NCC-003", Name="Cty CP CADIVI", Contact="Lê Thị Hoa", Phone="0274 3839 955", Email="info@cadivi.com.vn", Address="Bình Dương", Category="Vật tư điện", Rating=5, TotalOrders=25, TotalValue="623M ₫", Status="Đang hợp tác", LastOrder="12/03/2026" },
            new SupplierModel { Id=4, SupplierId="NCC-004", Name="Cty Nhựa Tiền Phong", Contact="Phạm Hoàng Nam", Phone="024 3827 2823", Email="contact@tienphuong.com", Address="Hải Phòng", Category="Vật tư cơ khí", Rating=4, TotalOrders=18, TotalValue="312M ₫", Status="Đang hợp tác", LastOrder="05/03/2026" },
            new SupplierModel { Id=5, SupplierId="NCC-005", Name="Cty Kim Long Hardware", Contact="Hoàng Thị Lan", Phone="028 3855 4321", Email="kimlong@hardware.vn", Address="Q.5, TP.HCM", Category="Phụ kiện", Rating=3, TotalOrders=8, TotalValue="87M ₫", Status="Tạm dừng", LastOrder="20/01/2026" }
        );

        // ── StorageZone seed ─────────────────────────────────────────────────
        modelBuilder.Entity<StorageZone>().HasData(
            new StorageZone { Id=1, Code="A", Name="Khu vực A", Color="#3b82f6", Description="Xi măng, thép, gạch, vật liệu xây dựng" },
            new StorageZone { Id=2, Code="B", Name="Khu vực B", Color="#10b981", Description="Dây điện, phụ kiện điện, bu lông, ống nước" },
            new StorageZone { Id=3, Code="C", Name="Khu vực C", Color="#8b5cf6", Description="Sơn nước, hóa chất công nghiệp, nhớt máy" }
        );

        // ── StorageRack seed ─────────────────────────────────────────────────
        modelBuilder.Entity<StorageRack>().HasData(
            new StorageRack { Id=1, ZoneId=1, Code="A1", Name="Kệ A1", Description="Xi măng & vôi" },
            new StorageRack { Id=2, ZoneId=1, Code="A2", Name="Kệ A2", Description="Ống PVC & phụ kiện" },
            new StorageRack { Id=3, ZoneId=1, Code="A3", Name="Kệ A3", Description="Gạch & đá" },
            new StorageRack { Id=4, ZoneId=2, Code="B1", Name="Kệ B1", Description="Dây & cáp điện" },
            new StorageRack { Id=5, ZoneId=2, Code="B2", Name="Kệ B2", Description="Phụ kiện cơ khí" },
            new StorageRack { Id=6, ZoneId=2, Code="B3", Name="Kệ B3", Description="Dự phòng điện" },
            new StorageRack { Id=7, ZoneId=3, Code="C1", Name="Kệ C1", Description="Sơn nước" },
            new StorageRack { Id=8, ZoneId=3, Code="C2", Name="Kệ C2", Description="Hóa chất" }
        );

        // ── StorageShelf seed (4 tầng/kệ) ───────────────────────────────────
        string[] levels = ["I", "II", "III", "IV"];
        var shelfList = new List<StorageShelf>();
        int sid = 1;
        for (int rId = 1; rId <= 8; rId++)
            for (int lv = 0; lv < 4; lv++)
                shelfList.Add(new StorageShelf { Id=sid++, RackId=rId, Level=levels[lv], SortOrder=lv+1 });
        modelBuilder.Entity<StorageShelf>().HasData(shelfList);

        // ── StorageCell seed (4 ô/tầng, một số được gán vật tư) ─────────────
        var cellList = new List<StorageCell>();
        int cid = 1;
        for (int s = 1; s <= 32; s++)
            for (int p = 1; p <= 4; p++)
                cellList.Add(new StorageCell { Id=cid++, ShelfId=s, Position=p.ToString() });

        // Helper gán vật tư vào ô
        void SetCell(int cellId, int matId, string code, string name, string unit,
                     int qty, int minQty, string batch, DateTime? expiry, decimal price)
        {
            var c = cellList.First(x => x.Id == cellId);
            c.MaterialId = matId; c.MaterialCode = code; c.MaterialName = name;
            c.MaterialUnit = unit; c.MaterialQty = qty; c.MaterialMinQty = minQty;
            c.MaterialBatch = batch; c.MaterialExpiry = expiry; c.MaterialPrice = price;
        }

        // Shelf 1 = Rack A1 Tầng I → Cell 1-4
        SetCell(1,  1,"VT-0001","Xi măng Hà Tiên PCB40",    "Bao 50kg",    1250,500,"LÔ-2026-01",new DateTime(2027,3,15),   95000m);
        SetCell(2,  2,"VT-0002","Sắt thép phi 12",           "Thanh 11.7m",  320,100,"LÔ-2026-02",new DateTime(2028,1,10),  185000m);
        // Shelf 2 = Rack A1 Tầng II → Cell 5-8
        SetCell(5,  7,"VT-0007","Gạch men 60x60 trắng bóng","Thùng 4 viên", 243, 80,"LÔ-2026-07",new DateTime(2030,1,1),   235000m);
        // Shelf 5 = Rack A2 Tầng I → Cell 17-20
        SetCell(17, 4,"VT-0004","Ống nước PVC Ø90mm",       "Cây 4m",       560,200,"LÔ-2026-04",new DateTime(2030,12,31), 145000m);
        // Shelf 13 = Rack B1 Tầng I → Cell 49-52
        SetCell(49, 3,"VT-0003","Dây điện CVV 2.5mm",        "Cuộn 100m",    12, 50,"LÔ-2026-03",new DateTime(2029,6,30), 1250000m);
        // Shelf 14 = Rack B1 Tầng II → Cell 53-56
        SetCell(53, 5,"VT-0005","Bu lông M12 x 50",          "Hộp 100 cái",   8, 30,"LÔ-2026-05",new DateTime(2028,5,20),   45000m);
        // Shelf 17 = Rack B2 Tầng I → Cell 65-68
        SetCell(65, 8,"VT-0008","Dây điện CVV 1.5mm",        "Cuộn 100m",    28, 40,"LÔ-2026-08",new DateTime(2027,11,15), 780000m);
        // Shelf 18 = Rack B2 Tầng II → Cell 69-72
        SetCell(69, 9,"VT-0009","Ổ cắm điện 3 lỗ",           "Cái",           8, 30,"LÔ-2026-09",new DateTime(2028,8,8),    35000m);
        // Shelf 25 = Rack C1 Tầng I → Cell 97-100
        SetCell(97, 6,"VT-0006","Sơn nước Dulux trắng 18L", "Thùng",        87, 40,"LÔ-2026-06",new DateTime(2026,9,20), 1350000m);
        // Shelf 26 = Rack C1 Tầng II → Cell 101-104
        SetCell(101,10,"VT-0010","Nhớt máy 10W-40",          "Lít",          20, 60,"LÔ-2026-10",new DateTime(2026,10,1),   75000m);

        modelBuilder.Entity<StorageCell>().HasData(cellList);
    }
}


