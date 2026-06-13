using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedProducts_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "IsDeleted", "Name", "Price", "Stock", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), new DateTime(2025, 6, 13, 0, 0, 0, System.DateTimeKind.Utc), "Comfortable cotton t-shirt", true, false, "Red T-Shirt", 19.99m, 100, null },
                    { new Guid("11111111-0000-0000-0000-000000000002"), new DateTime(2025, 6, 13, 0, 0, 0, System.DateTimeKind.Utc), "Slim fit denim jeans", true, false, "Blue Jeans", 49.50m, 50, null },
                    { new Guid("11111111-0000-0000-0000-000000000003"), new DateTime(2025, 6, 13, 0, 0, 0, System.DateTimeKind.Utc), "Lightweight running shoes", true, false, "Running Shoes", 89.00m, 30, null },
                    { new Guid("11111111-0000-0000-0000-000000000004"), new DateTime(2025, 6, 13, 0, 0, 0, System.DateTimeKind.Utc), "Ceramic mug 350ml", true, false, "Coffee Mug", 9.25m, 200, null },
                    { new Guid("11111111-0000-0000-0000-000000000005"), new DateTime(2025, 6, 13, 0, 0, 0, System.DateTimeKind.Utc), "Ergonomic wireless mouse", true, false, "Wireless Mouse", 29.99m, 75, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValues: new object[]
                {
                    new Guid("11111111-0000-0000-0000-000000000001"),
                    new Guid("11111111-0000-0000-0000-000000000002"),
                    new Guid("11111111-0000-0000-0000-000000000003"),
                    new Guid("11111111-0000-0000-0000-000000000004"),
                    new Guid("11111111-0000-0000-0000-000000000005")
                });
        }
    }
}
