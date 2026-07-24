using System;
using AeroFly.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AeroFly.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260724120000_SecurePaymentsSeatsAndSessions")]
public partial class SecurePaymentsSeatsAndSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PointsTransactions_BookingId",
            table: "PointsTransactions");

        migrationBuilder.AddColumn<DateTime>(
            name: "SeatHoldExpiresAt",
            table: "Bookings",
            type: ActiveProvider.Contains("SqlServer") ? "datetime2" : "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "SeatsReserved",
            table: "Bookings",
            type: ActiveProvider.Contains("SqlServer") ? "bit" : "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "SecurityStamp",
            table: "Users",
            type: ActiveProvider.Contains("SqlServer") ? "nvarchar(64)" : "TEXT",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "Users",
            type: ActiveProvider.Contains("SqlServer") ? "bit" : "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "StripeRefundId",
            table: "Payments",
            type: ActiveProvider.Contains("SqlServer") ? "nvarchar(100)" : "TEXT",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RefundStatus",
            table: "Payments",
            type: ActiveProvider.Contains("SqlServer") ? "nvarchar(30)" : "TEXT",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RefundFailureReason",
            table: "Payments",
            type: ActiveProvider.Contains("SqlServer") ? "nvarchar(500)" : "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "RefundedAt",
            table: "Payments",
            type: ActiveProvider.Contains("SqlServer") ? "datetime2" : "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FlightId",
            table: "Tickets",
            type: ActiveProvider.Contains("SqlServer") ? "int" : "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsUsed",
            table: "Tickets",
            type: ActiveProvider.Contains("SqlServer") ? "bit" : "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "UsedAt",
            table: "Tickets",
            type: ActiveProvider.Contains("SqlServer") ? "datetime2" : "TEXT",
            nullable: true);

        if (ActiveProvider.Contains("SqlServer"))
        {
            migrationBuilder.Sql("UPDATE t SET FlightId = b.FlightId FROM Tickets t INNER JOIN Bookings b ON b.BookingId = t.BookingId;");
            migrationBuilder.Sql("UPDATE Users SET SecurityStamp = REPLACE(CONVERT(varchar(36), NEWID()), '-', '') WHERE SecurityStamp = '';");
        }
        else
        {
            migrationBuilder.Sql("UPDATE Tickets SET FlightId = (SELECT FlightId FROM Bookings WHERE Bookings.BookingId = Tickets.BookingId);");
            migrationBuilder.Sql("UPDATE Users SET SecurityStamp = lower(hex(randomblob(16))) WHERE SecurityStamp = '';");
        }

        migrationBuilder.AlterColumn<int>(
            name: "FlightId",
            table: "Tickets",
            type: ActiveProvider.Contains("SqlServer") ? "int" : "INTEGER",
            nullable: false,
            oldClrType: typeof(int),
            oldType: ActiveProvider.Contains("SqlServer") ? "int" : "INTEGER",
            oldNullable: true);

        migrationBuilder.Sql("UPDATE Bookings SET SeatsReserved = 1 WHERE Status IN ('Confirmed', 'Completed');");

        migrationBuilder.CreateTable(
            name: "StripeWebhookEvents",
            columns: table => new
            {
                EventId = table.Column<string>(
                    type: ActiveProvider.Contains("SqlServer") ? "nvarchar(100)" : "TEXT",
                    maxLength: 100,
                    nullable: false),
                EventType = table.Column<string>(
                    type: ActiveProvider.Contains("SqlServer") ? "nvarchar(100)" : "TEXT",
                    maxLength: 100,
                    nullable: false),
                ProcessedAt = table.Column<DateTime>(
                    type: ActiveProvider.Contains("SqlServer") ? "datetime2" : "TEXT",
                    nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_StripeWebhookEvents", x => x.EventId));

        migrationBuilder.CreateIndex(
            name: "IX_PointsTransactions_BookingId",
            table: "PointsTransactions",
            column: "BookingId",
            filter: ActiveProvider.Contains("SqlServer") ? "[BookingId] IS NOT NULL" : null);

        migrationBuilder.CreateIndex(
            name: "IX_Payments_BookingId",
            table: "Payments",
            column: "BookingId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Payments_TransactionRef",
            table: "Payments",
            column: "TransactionRef",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Tickets_FlightId_SeatNum",
            table: "Tickets",
            columns: new[] { "FlightId", "SeatNum" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Tickets_QrCode",
            table: "Tickets",
            column: "QrCode",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Tickets_Flights_FlightId",
            table: "Tickets",
            column: "FlightId",
            principalTable: "Flights",
            principalColumn: "FlightId",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StripeWebhookEvents");
        migrationBuilder.DropForeignKey(name: "FK_Tickets_Flights_FlightId", table: "Tickets");
        migrationBuilder.DropIndex(name: "IX_Tickets_FlightId_SeatNum", table: "Tickets");
        migrationBuilder.DropIndex(name: "IX_Tickets_QrCode", table: "Tickets");
        migrationBuilder.DropIndex(name: "IX_Payments_BookingId", table: "Payments");
        migrationBuilder.DropIndex(name: "IX_Payments_TransactionRef", table: "Payments");
        migrationBuilder.DropIndex(name: "IX_PointsTransactions_BookingId", table: "PointsTransactions");
        migrationBuilder.DropColumn(name: "FlightId", table: "Tickets");
        migrationBuilder.DropColumn(name: "IsUsed", table: "Tickets");
        migrationBuilder.DropColumn(name: "UsedAt", table: "Tickets");
        migrationBuilder.DropColumn(name: "StripeRefundId", table: "Payments");
        migrationBuilder.DropColumn(name: "RefundStatus", table: "Payments");
        migrationBuilder.DropColumn(name: "RefundFailureReason", table: "Payments");
        migrationBuilder.DropColumn(name: "RefundedAt", table: "Payments");
        migrationBuilder.DropColumn(name: "SeatHoldExpiresAt", table: "Bookings");
        migrationBuilder.DropColumn(name: "SeatsReserved", table: "Bookings");
        migrationBuilder.DropColumn(name: "SecurityStamp", table: "Users");
        migrationBuilder.DropColumn(name: "MustChangePassword", table: "Users");
        migrationBuilder.CreateIndex(
            name: "IX_PointsTransactions_BookingId",
            table: "PointsTransactions",
            column: "BookingId",
            unique: true,
            filter: "[BookingId] IS NOT NULL");
    }
}
