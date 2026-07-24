using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroFly.Web.Migrations
{
    /// <inheritdoc />
    public partial class AlignBusinessLogicRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "BookingId",
                table: "PointsTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SeatClasses",
                keyColumn: "ClassId",
                keyValue: 2,
                column: "ClassMultiplier",
                value: 2.0m);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_BookingId",
                table: "PointsTransactions",
                column: "BookingId",
                unique: true,
                filter: "[BookingId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PointsTransactions_Bookings_BookingId",
                table: "PointsTransactions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointsTransactions_Bookings_BookingId",
                table: "PointsTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PointsTransactions_BookingId",
                table: "PointsTransactions");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "PointsTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.UpdateData(
                table: "SeatClasses",
                keyColumn: "ClassId",
                keyValue: 2,
                column: "ClassMultiplier",
                value: 2.5m);
        }
    }
}
