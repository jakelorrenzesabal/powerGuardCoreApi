using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace powerGuardCoreApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAccountRoomJunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountRooms_Accounts_AccountsAccountId",
                table: "AccountRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountRooms_Rooms_RoomsRoomId",
                table: "AccountRooms");

            migrationBuilder.RenameColumn(
                name: "RoomsRoomId",
                table: "AccountRooms",
                newName: "RoomId");

            migrationBuilder.RenameColumn(
                name: "AccountsAccountId",
                table: "AccountRooms",
                newName: "AccountId");

            migrationBuilder.RenameIndex(
                name: "IX_AccountRooms_RoomsRoomId",
                table: "AccountRooms",
                newName: "IX_AccountRooms_RoomId");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "AccountRooms",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountRooms_Accounts_AccountId",
                table: "AccountRooms",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountRooms_Rooms_RoomId",
                table: "AccountRooms",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "RoomId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountRooms_Accounts_AccountId",
                table: "AccountRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountRooms_Rooms_RoomId",
                table: "AccountRooms");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "AccountRooms");

            migrationBuilder.RenameColumn(
                name: "RoomId",
                table: "AccountRooms",
                newName: "RoomsRoomId");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "AccountRooms",
                newName: "AccountsAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_AccountRooms_RoomId",
                table: "AccountRooms",
                newName: "IX_AccountRooms_RoomsRoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountRooms_Accounts_AccountsAccountId",
                table: "AccountRooms",
                column: "AccountsAccountId",
                principalTable: "Accounts",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountRooms_Rooms_RoomsRoomId",
                table: "AccountRooms",
                column: "RoomsRoomId",
                principalTable: "Rooms",
                principalColumn: "RoomId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
