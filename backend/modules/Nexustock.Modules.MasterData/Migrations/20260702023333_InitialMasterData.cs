using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nexustock.Modules.MasterData.Migrations
{
    /// <inheritdoc />
    public partial class InitialMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    success_rows = table.Column<int>(type: "integer", nullable: false),
                    error_rows = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_batches_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "partners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    partner_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    tax_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partners", x => x.id);
                    table.ForeignKey(
                        name: "FK_partners_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reason_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reason_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_reason_codes_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_configs",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fifo_policy_level = table.Column<int>(type: "integer", nullable: false),
                    lot_no_pattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    allow_negative_stock = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_configs", x => x.tenant_id);
                    table.ForeignKey(
                        name: "FK_tenant_configs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "uoms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uoms", x => x.id);
                    table.ForeignKey(
                        name: "FK_uoms_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.id);
                    table.ForeignKey(
                        name: "FK_warehouses_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_batch_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_index = table.Column<int>(type: "integer", nullable: false),
                    raw_data = table.Column<string>(type: "jsonb", nullable: true),
                    is_valid = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batch_rows", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_batch_rows_import_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    base_uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_products_uoms_base_uom_id",
                        column: x => x.base_uom_id,
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "storage_zones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    zone_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    temperature_limit = table.Column<decimal>(type: "numeric", nullable: true),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_zones", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_zones_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_storage_zones_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "packages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    uom_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversion_factor = table.Column<decimal>(type: "numeric", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_packages", x => x.id);
                    table.ForeignKey(
                        name: "FK_packages_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_packages_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_packages_uoms_uom_id",
                        column: x => x.uom_id,
                        principalTable: "uoms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_configs",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iqc_check_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vendor_inner_lot_ctl = table.Column<bool>(type: "boolean", nullable: false),
                    is_wafer = table.Column<bool>(type: "boolean", nullable: false),
                    lot_validation_regex = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    min_stock = table.Column<decimal>(type: "numeric", nullable: false),
                    max_stock = table.Column<decimal>(type: "numeric", nullable: false),
                    weight_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rotation_speed = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    track_serial = table.Column<bool>(type: "boolean", nullable: false),
                    length = table.Column<decimal>(type: "numeric", nullable: false),
                    width = table.Column<decimal>(type: "numeric", nullable: false),
                    height = table.Column<decimal>(type: "numeric", nullable: false),
                    weight = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_configs", x => x.product_id);
                    table.ForeignKey(
                        name: "FK_product_configs_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_configs_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "storage_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    max_capacity = table.Column<decimal>(type: "numeric", nullable: false),
                    max_volume = table.Column<decimal>(type: "numeric", nullable: false),
                    x_coord = table.Column<int>(type: "integer", nullable: false),
                    y_coord = table.Column<int>(type: "integer", nullable: false),
                    z_coord = table.Column<int>(type: "integer", nullable: false),
                    length = table.Column<decimal>(type: "numeric", nullable: false),
                    width = table.Column<decimal>(type: "numeric", nullable: false),
                    height = table.Column<decimal>(type: "numeric", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    lock_reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_storage_locations_storage_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "storage_zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_storage_locations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "tenants",
                columns: new[] { "id", "code", "created_at", "created_by", "is_active", "name", "row_version", "updated_at", "updated_by" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "DEFAULT-TENANT", new DateTimeOffset(new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", true, "Default Tenant", 1, null, null });

            migrationBuilder.InsertData(
                table: "reason_codes",
                columns: new[] { "id", "code", "created_at", "created_by", "description", "is_active", "reason_type", "row_version", "tenant_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000051"), "HOLD-QC", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4482), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", "Chờ QC kiểm tra chất lượng", true, "HOLD", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000000052"), "ADJ-COUNT", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4485), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", "Điều chỉnh số lượng kiểm kê", true, "INVENTORY_ADJUSTMENT", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.InsertData(
                table: "tenant_configs",
                columns: new[] { "tenant_id", "allow_negative_stock", "fifo_policy_level", "lot_no_pattern", "updated_at", "updated_by" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), false, 2, "{YYYY}{MM}{DD}-{SEQ}", null, null });

            migrationBuilder.InsertData(
                table: "uoms",
                columns: new[] { "id", "code", "created_at", "created_by", "is_active", "name", "row_version", "tenant_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000011"), "PCS", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4380), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", true, "Cái", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000000012"), "BOX", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4382), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", true, "Hộp", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000000013"), "PALLET", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4384), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", true, "Pallet", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.InsertData(
                table: "warehouses",
                columns: new[] { "id", "code", "created_at", "created_by", "description", "is_active", "name", "row_version", "tenant_id", "updated_at", "updated_by" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000021"), "WH-MAIN", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4396), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", null, true, "Kho chính", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null });

            migrationBuilder.InsertData(
                table: "storage_zones",
                columns: new[] { "id", "code", "created_at", "created_by", "is_locked", "name", "row_version", "temperature_limit", "tenant_id", "updated_at", "updated_by", "warehouse_id", "zone_type" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000031"), "ZONE-STORAGE", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4409), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", false, "Khu lưu trữ chính", 1, null, new Guid("00000000-0000-0000-0000-000000000001"), null, null, new Guid("00000000-0000-0000-0000-000000000021"), "STORAGE" },
                    { new Guid("00000000-0000-0000-0000-000000000032"), "ZONE-QC", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4412), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", false, "Khu kiểm tra chất lượng", 1, null, new Guid("00000000-0000-0000-0000-000000000001"), null, null, new Guid("00000000-0000-0000-0000-000000000021"), "QC" },
                    { new Guid("00000000-0000-0000-0000-000000000033"), "ZONE-STAGING", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4414), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", false, "Khu trung chuyển", 1, null, new Guid("00000000-0000-0000-0000-000000000001"), null, null, new Guid("00000000-0000-0000-0000-000000000021"), "STAGING" }
                });

            migrationBuilder.InsertData(
                table: "storage_locations",
                columns: new[] { "id", "code", "created_at", "created_by", "height", "is_active", "is_locked", "length", "lock_reason_code", "max_capacity", "max_volume", "row_version", "tenant_id", "updated_at", "updated_by", "width", "x_coord", "y_coord", "z_coord", "zone_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000041"), "LOC-A-01", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4426), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", 0.00m, true, false, 0.00m, null, 1000m, 1000m, 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null, 0.00m, 1, 1, 1, new Guid("00000000-0000-0000-0000-000000000031") },
                    { new Guid("00000000-0000-0000-0000-000000000042"), "LOC-A-02", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4435), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", 0.00m, true, false, 0.00m, null, 1000m, 1000m, 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null, 0.00m, 1, 2, 1, new Guid("00000000-0000-0000-0000-000000000031") },
                    { new Guid("00000000-0000-0000-0000-000000000043"), "LOC-QC-01", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4463), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", 0.00m, true, false, 0.00m, null, 500m, 500m, 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null, 0.00m, 2, 1, 1, new Guid("00000000-0000-0000-0000-000000000032") },
                    { new Guid("00000000-0000-0000-0000-000000000044"), "LOC-QC-02", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4466), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", 0.00m, true, false, 0.00m, null, 500m, 500m, 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null, 0.00m, 2, 2, 1, new Guid("00000000-0000-0000-0000-000000000032") },
                    { new Guid("00000000-0000-0000-0000-000000000045"), "LOC-STG-01", new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4468), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", 0.00m, true, false, 0.00m, null, 2000m, 2000m, 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null, 0.00m, 3, 1, 1, new Guid("00000000-0000-0000-0000-000000000033") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_batch_rows_batch_id",
                table: "import_batch_rows",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_tenant_id",
                table: "import_batches",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_packages_uom_id",
                table: "packages",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "uq_packages_product_uom",
                table: "packages",
                columns: new[] { "product_id", "uom_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_packages_tenant_barcode",
                table: "packages",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_partners_tenant_code",
                table: "partners",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_configs_tenant_id",
                table: "product_configs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_base_uom_id",
                table: "products",
                column: "base_uom_id");

            migrationBuilder.CreateIndex(
                name: "uq_products_tenant_barcode",
                table: "products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_products_tenant_code",
                table: "products",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_reason_codes_tenant_type_code",
                table: "reason_codes",
                columns: new[] { "tenant_id", "reason_type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_storage_locations_code",
                table: "storage_locations",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_locations_zone_id",
                table: "storage_locations",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_storage_zones_warehouse_id",
                table: "storage_zones",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_storage_zones_tenant_warehouse_code",
                table: "storage_zones",
                columns: new[] { "tenant_id", "warehouse_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tenants_active",
                table: "tenants",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "uq_tenants_code",
                table: "tenants",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_uoms_tenant_code",
                table: "uoms",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_warehouses_tenant_code",
                table: "warehouses",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_batch_rows");

            migrationBuilder.DropTable(
                name: "packages");

            migrationBuilder.DropTable(
                name: "partners");

            migrationBuilder.DropTable(
                name: "product_configs");

            migrationBuilder.DropTable(
                name: "reason_codes");

            migrationBuilder.DropTable(
                name: "storage_locations");

            migrationBuilder.DropTable(
                name: "tenant_configs");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "storage_zones");

            migrationBuilder.DropTable(
                name: "uoms");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
