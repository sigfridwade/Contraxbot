﻿using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace NadekoBot.Migrations
{
    public partial class patreoncurrencypercent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "PatreonCurrencyPerCent",
                table: "BotConfig",
                nullable: false,
                defaultValue: 1f);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PatreonCurrencyPerCent",
                table: "BotConfig");
        }
    }
}
