using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationMembers_Users_UserId1",
                table: "ConversationMembers");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMembers_UserId1",
                table: "ConversationMembers");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "ConversationMembers");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "ConversationMembers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_UserId1",
                table: "ConversationMembers",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationMembers_Users_UserId1",
                table: "ConversationMembers",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
