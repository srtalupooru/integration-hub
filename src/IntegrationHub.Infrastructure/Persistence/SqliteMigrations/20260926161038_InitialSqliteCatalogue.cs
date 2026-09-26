using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntegrationHub.Infrastructure.Persistence.SqliteMigrations
{
    /// <inheritdoc />
    public partial class InitialSqliteCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Integrations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BusinessDomain = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Criticality = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalJson = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalDefinition = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionHash = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationJson = table.Column<string>(type: "TEXT", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageEntity",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEntity", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Systems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessDomain = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Systems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DependencyEntity",
                columns: table => new
                {
                    IntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    TargetIntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DependencyEntity", x => new { x.IntegrationId, x.TargetIntegrationId });
                    table.ForeignKey(
                        name: "FK_DependencyEntity_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DependencyEntity_Integrations_TargetIntegrationId",
                        column: x => x.TargetIntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationEdgeEntity",
                columns: table => new
                {
                    IntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FromNodeId = table.Column<string>(type: "TEXT", nullable: false),
                    ToNodeId = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Protocol = table.Column<string>(type: "TEXT", nullable: false),
                    MessageName = table.Column<string>(type: "TEXT", nullable: true),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationEdgeEntity", x => new { x.IntegrationId, x.Id });
                    table.ForeignKey(
                        name: "FK_IntegrationEdgeEntity_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryEntity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryEntity_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunbookEntity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunbookEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunbookEntity_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TagEntity",
                columns: table => new
                {
                    IntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagEntity", x => new { x.IntegrationId, x.Value });
                    table.ForeignKey(
                        name: "FK_TagEntity_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Versions",
                columns: table => new
                {
                    IntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionHash = table.Column<string>(type: "TEXT", nullable: false),
                    Format = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalDefinition = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Versions", x => new { x.IntegrationId, x.Revision });
                    table.ForeignKey(
                        name: "FK_Versions_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationMessages",
                columns: table => new
                {
                    IntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    MessageName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Producer = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    TopicOrQueue = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationMessages", x => new { x.IntegrationId, x.MessageName });
                    table.ForeignKey(
                        name: "FK_IntegrationMessages_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntegrationMessages_MessageEntity_MessageName",
                        column: x => x.MessageName,
                        principalTable: "MessageEntity",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Nodes",
                columns: table => new
                {
                    IntegrationId = table.Column<string>(type: "TEXT", nullable: false),
                    Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Technology = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    SystemId = table.Column<string>(type: "TEXT", nullable: true),
                    SharedResourceId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsSource = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDestination = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => new { x.IntegrationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Nodes_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Nodes_Systems_SystemId",
                        column: x => x.SystemId,
                        principalTable: "Systems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DependencyEntity_TargetIntegrationId",
                table: "DependencyEntity",
                column: "TargetIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationMessages_MessageName",
                table: "IntegrationMessages",
                column: "MessageName");

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_Status_BusinessDomain",
                table: "Integrations",
                columns: new[] { "Status", "BusinessDomain" });

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_UpdatedAt",
                table: "Integrations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_SharedResourceId",
                table: "Nodes",
                column: "SharedResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_SystemId",
                table: "Nodes",
                column: "SystemId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryEntity_IntegrationId",
                table: "RepositoryEntity",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_RunbookEntity_IntegrationId",
                table: "RunbookEntity",
                column: "IntegrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DependencyEntity");

            migrationBuilder.DropTable(
                name: "IntegrationEdgeEntity");

            migrationBuilder.DropTable(
                name: "IntegrationMessages");

            migrationBuilder.DropTable(
                name: "Nodes");

            migrationBuilder.DropTable(
                name: "RepositoryEntity");

            migrationBuilder.DropTable(
                name: "RunbookEntity");

            migrationBuilder.DropTable(
                name: "TagEntity");

            migrationBuilder.DropTable(
                name: "Versions");

            migrationBuilder.DropTable(
                name: "MessageEntity");

            migrationBuilder.DropTable(
                name: "Systems");

            migrationBuilder.DropTable(
                name: "Integrations");
        }
    }
}
