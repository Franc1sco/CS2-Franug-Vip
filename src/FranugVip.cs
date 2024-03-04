using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using System.Text.Json.Serialization;

namespace FranugVip;

public class ConfigGen : BasePluginConfig
{
    [JsonPropertyName("AccessFlag")] public string AccessFlag { get; set; } = "";
    [JsonPropertyName("DatabaseType")]
    public string DatabaseType { get; set; } = "SQLite";
    [JsonPropertyName("DatabaseFilePath")]
    public string DatabaseFilePath { get; set; } = "/csgo/addons/counterstrikesharp/plugins/FranugVip/franug-vip-db.sqlite";
    [JsonPropertyName("DatabaseHost")]
    public string DatabaseHost { get; set; } = "";
    [JsonPropertyName("DatabasePort")]
    public int DatabasePort { get; set; }
    [JsonPropertyName("DatabaseUser")]
    public string DatabaseUser { get; set; } = "";
    [JsonPropertyName("DatabasePassword")]
    public string DatabasePassword { get; set; } = "";
    [JsonPropertyName("DatabaseName")]
    public string DatabaseName { get; set; } = "";
    [JsonPropertyName("Comment")]
    public string Comment { get; set; } = "Use SQLite or MySQL as Database Type.";
    [JsonPropertyName("DebugEnabled")] public bool DebugEnabled { get; set; } = true;
    [JsonPropertyName("TestVipTime")] public int TestVipTime { get; set; } = 60;
    [JsonPropertyName("VipHealth")] public int VipHealth { get; set; } = 120;
    [JsonPropertyName("VipArmor")] public int VipArmor { get; set; } = 120;
}

public class FranugVip : BasePlugin, IPluginConfig<ConfigGen>
{
    public override string ModuleName => "Franug Vip";
    public override string ModuleAuthor => "Franc1sco Franug";
    public override string ModuleVersion => "0.0.2";

    public ConfigGen Config { get; set; } = null!;
    public void OnConfigParsed(ConfigGen config) { Config = config; }

    private SqliteConnection? connectionSQLITE = null;
    internal static MySqlConnection? connectionMySQL = null;
    private readonly Dictionary<int, bool> bVip = new();
    private readonly Dictionary<int, bool> bArmor = new();
    private readonly Dictionary<int, bool> bHp = new();
    private readonly Dictionary<int, int> bFov = new();
    private readonly Dictionary<int, bool> bBhop = new();
    private readonly Dictionary<int, bool> bHS = new();

    public override void Load(bool hotReload)
    {
        createDB();
        if (hotReload)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player.IsBot || !player.IsValid)
                {
                    continue;

                }
                else
                {
                    bVip.Add((int)player.Index, false);
                    bArmor.Add((int)player.Index, true);
                    bHp.Add((int)player.Index, true);
                    bFov.Add((int)player.Index, 90);
                    bBhop.Add((int)player.Index, true);
                    bHS.Add((int)player.Index, true);
                    getPlayerData(player);
                }
            }
        }
        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;

            if (player.IsBot || !player.IsValid)
            {
                return HookResult.Continue;

            }
            else
            {
                bVip.Add((int)player.Index, false);
                bArmor.Add((int)player.Index, true);
                bHp.Add((int)player.Index, true);
                bFov.Add((int)player.Index, 90);
                bBhop.Add((int)player.Index, true);
                bHS.Add((int)player.Index, true);
                getPlayerData(player);
                return HookResult.Continue;
            }
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;

            if (player.IsBot || !player.IsValid)
            {
                return HookResult.Continue;

            }
            else
            {
                if (bVip.ContainsKey((int)player.Index))
                {
                    bVip.Remove((int)player.Index);
                }
                if (bArmor.ContainsKey((int)player.Index))
                {
                    bArmor.Remove((int)player.Index);
                }
                if (bHp.ContainsKey((int)player.Index))
                {
                    bHp.Remove((int)player.Index);
                }
                if (bFov.ContainsKey((int)player.Index))
                {
                    bFov.Remove((int)player.Index);
                }
                if (bBhop.ContainsKey((int)player.Index))
                {
                    bBhop.Remove((int)player.Index);
                }
                if (bHS.ContainsKey((int)player.Index))
                {
                    bHS.Remove((int)player.Index);
                }
                return HookResult.Continue;
            }
        });

        RegisterEventHandler<EventPlayerSpawn>(eventPlayerSpawn);
        RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var player in Utilities.GetPlayers()
                         .Where(player => player is { IsValid: true, IsBot: false, PawnIsAlive: true }))
            {
                if (bVip[(int)player.Index] != true || bBhop[(int)player.Index] != true) continue;

                OnTick(player);
            }
        });
    }

    private HookResult eventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        Server.NextFrame(() =>
        {
            if (!IsPlayerValid(player)) { 
                return;
            }

            if (bVip[(int)player.Index])
            {
                var playerPawn = player.PlayerPawn.Value;

                if (playerPawn == null) return;

                if (bHp[(int)player.Index])
                {
                    playerPawn.Health = Config.VipHealth;
                    playerPawn.MaxHealth = Config.VipHealth;

                    Utilities.SetStateChanged(playerPawn, "CBaseEntity", "m_iHealth");
                }

                if (bArmor[(int)player.Index])
                {

                    if (playerPawn.ItemServices != null)
                        new CCSPlayer_ItemServices(playerPawn.ItemServices.Handle).HasHelmet = true;

                    playerPawn.ArmorValue = Config.VipArmor;

                }

                if (bHS[(int)player.Index])
                {
                    player.GiveNamedItem("weapon_healthshot");
                }
                ChangeFov(player, bFov[(int)player.Index]);

                return;
            }

            ChangeFov(player, 90);
        });

        return HookResult.Continue;
    }

    private void OnTick(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn != null)
        {
            var flags = (PlayerFlags)playerPawn.Flags;
            var buttons = player.Buttons;

            if (buttons.HasFlag(PlayerButtons.Jump) && flags.HasFlag(PlayerFlags.FL_ONGROUND) &&
                !playerPawn.MoveType.HasFlag(MoveType_t.MOVETYPE_LADDER))
                playerPawn.AbsVelocity.Z = 300;
        }
    }

    [ConsoleCommand("css_testvip", "Test vip.")]
    public void OnTestVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (bVip[(int)player.Index])
        {
            info.ReplyToCommand("You already are a vip");
            return;
        }

        _ = testCheckAsync(player, info);
    }

    private async Task testCheckAsync(CCSPlayerController player, CommandInfo info)
    {
        if (Config.DatabaseType != "MySQL")
        {
            var result = await RecordTestExistsSQLite(player);

            //Console.WriteLine("resultado es " + exists.Result);
            if (result)
            {
                info.ReplyToCommand("You already used viptest");
            }
            else
            {
                result = await RecordTestAllExistsSQLite(player);
                if (result)
                {
                    info.ReplyToCommand("Viptest is already on use for someone");
                }
                else
                {

                    _ = InsertTestQueryDataSQLite((int)player.SteamID);
                    bVip[(int)player.Index] = true;
                    info.ReplyToCommand("You got vip access");
                }
            }
        }
        else
        {
            var result = await RecordTestExistsMySQL(player);

            if (result)
            {
                info.ReplyToCommand("You already used viptest");
            }
            else
            {
                result = await RecordTestAllExistsMySQL(player);
                if (result)
                {
                    info.ReplyToCommand("Viptest is already on use for someone");
                }
                else
                {

                    _ = InsertTestQueryDataMySQL((int)player.SteamID);
                    bVip[(int)player.Index] = true;
                    info.ReplyToCommand("You got vip access");
                }
            }
        }
    }

    [ConsoleCommand("css_addvip", "Add an vip.")]
    [CommandHelper(2, "<target or steamid> <time in minutes , 0 = permanent>")]
    [RequiresPermissions("@css/root")]
    public void OnAddVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        var steamId = info.GetArg(1).ToString();
        Console.WriteLine(steamId);
        if (!long.TryParse(steamId, out long result))
        {
            info.ReplyToCommand("steamid must be a number");
            return;
        }

        var time = info.GetArg(2).ToString();
        if (!long.TryParse(time, out long result2))
        {
            info.ReplyToCommand("time must be a number");
            return;
        }
        info.ReplyToCommand("steamid "+ result + " added for "+result2+ " minutes.");

        if (result2 == 0) updateVipAsync(result, 0);
        else updateVipAsync(result, CalculateEndTimeInSeconds(result2));
    }

    [ConsoleCommand("css_removevip", "Remove an vip.")]
    [CommandHelper(1, "<target or steamid>")]
    [RequiresPermissions("@css/root")]
    public void OnRemoveVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        var steamId = info.GetArg(1);
        if (!long.TryParse(steamId, out long steamidformat))
        {
            info.ReplyToCommand("steamid must be a number");
            return;
        }

        _ = vipCheckAsync(player, info, steamidformat);
    }

    private async Task vipCheckAsync(CCSPlayerController player, CommandInfo info, long steamid)
    {
        if (Config.DatabaseType != "MySQL")
        {
            var result = await RecordVipExistsSQLite(steamid);

            //Console.WriteLine("resultado es " + exists.Result);
            if (!result)
            {
                info.ReplyToCommand("Vip with steamid "+steamid+ " not found");
            }
            else
            {
                info.ReplyToCommand("steamid " + steamid + " removed.");
                _ = removeVipAsync(steamid);
            }
        }
        else
        {
            var result = await RecordVipExistsMySQL(steamid);

            if (!result)
            {
                info.ReplyToCommand("Vip with steamid " + steamid + " not found");
            }
            else
            {
                info.ReplyToCommand("steamid " + steamid + " removed.");
                _ = removeVipAsync(steamid);
            }
        }
    }

    [ConsoleCommand("css_fov", "Set Fov.")]
    public void OnFovCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!bVip[(int)player.Index])
        {
            info.ReplyToCommand("You are not a vip");
            return;
        }
        FovMenu(player);
    }

    [ConsoleCommand("css_vip", "Vip menu.")]
    public void OnVipCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!bVip[(int)player.Index])
        {
            info.ReplyToCommand("You are not a vip");
            return;
        }
        var menu = new ChatMenu("Vip menu");
        var enable = "enabled";

        enable = bHp[(int)player.Index] ? "enabled" : "disabled";
        menu.AddMenuOption("Health - "+enable, (player, option) => {
            bHp[(int)player.Index] = !bHp[(int)player.Index];

            enable = bHp[(int)player.Index] ? "enabled" : "disabled";
            player.PrintToChat("Health is "+enable);
        });

        enable = bArmor[(int)player.Index] ? "enabled" : "disabled";
        menu.AddMenuOption("Armor - " + enable, (player, option) => {
            bArmor[(int)player.Index] = !bArmor[(int)player.Index];

            enable = bArmor[(int)player.Index] ? "enabled" : "disabled";
            player.PrintToChat("Armor is " + enable);
        });

        enable = bBhop[(int)player.Index] ? "enabled" : "disabled";
        menu.AddMenuOption("Bhop - " + enable, (player, option) => {
            bBhop[(int)player.Index] = !bBhop[(int)player.Index];

            enable = bBhop[(int)player.Index] ? "enabled" : "disabled";
            player.PrintToChat("Bhop is " + enable);
        });

        enable = bHS[(int)player.Index] ? "enabled" : "disabled";
        menu.AddMenuOption("Syringe - " + enable, (player, option) => {
            bHS[(int)player.Index] = !bHS[(int)player.Index];

            enable = bHS[(int)player.Index] ? "enabled" : "disabled";
            player.PrintToChat("Syringe is " + enable);
        });

        menu.AddMenuOption("Fov value: " + bFov[(int)player.Index], (player, option) => {
            FovMenu(player);
        });

        menu.PostSelectAction = PostSelectAction.Close;
        MenuManager.OpenChatMenu(player, menu);
    }

    private void FovMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Select Fov Value");
        menu.AddMenuOption("60", (player, option) => {
            player.PrintToChat("Fov changed to 60");
            ChangeFov(player, 60);
        });
        menu.AddMenuOption("70", (player, option) => {
            player.PrintToChat("Fov changed to 70");
            ChangeFov(player, 70);
        });
        menu.AddMenuOption("80", (player, option) => {
            player.PrintToChat("Fov changed to 80");
            ChangeFov(player, 80);
        });
        menu.AddMenuOption("90 - DEFAULT", (player, option) => {
            player.PrintToChat("Fov changed to 90");
            ChangeFov(player, 90);
        });
        menu.AddMenuOption("100", (player, option) => {
            player.PrintToChat("Fov changed to 100");
            ChangeFov(player, 100);
        });
        menu.AddMenuOption("110", (player, option) => {
            player.PrintToChat("Fov changed to 110");
            ChangeFov(player, 110);
        });
        menu.AddMenuOption("120", (player, option) => {
            player.PrintToChat("Fov changed to 120");
            ChangeFov(player, 120);
        });
        menu.PostSelectAction = PostSelectAction.Close;
        MenuManager.OpenChatMenu(player, menu);
    }

    private void ChangeFov(CCSPlayerController player, int fov)
    {
        player.DesiredFOV = (uint)fov;
        Utilities.SetStateChanged(player, "CBasePlayerController", "m_iDesiredFOV");
    }

    private void createDB()
    {
        if (Config.DatabaseType != "MySQL")
        {
            CreateTableSQLite();
        }
        else
        {
            CreateTableMySQL();
        }
    }

    private void CreateTableSQLite()
    {
        string dbFilePath = Server.GameDirectory + Config.DatabaseFilePath;

        var connectionString = $"Data Source={dbFilePath};";

        connectionSQLITE = new SqliteConnection(connectionString);

        connectionSQLITE.Open();

        var query = "CREATE TABLE IF NOT EXISTS franug_viplist (steamid varchar(32) NOT NULL, time int(64) NOT NULL, PRIMARY KEY(steamid));";

        using (SqliteCommand command = new SqliteCommand(query, connectionSQLITE))
        {
            command.CommandText = query;
            command.ExecuteNonQuery();
        }

        query = "CREATE TABLE IF NOT EXISTS franug_viptest (steamid varchar(32) NOT NULL, time int(64) NOT NULL, PRIMARY KEY(steamid));";

        using (SqliteCommand command = new SqliteCommand(query, connectionSQLITE))
        {
            command.CommandText = query;
            command.ExecuteNonQuery();
        }
        connectionSQLITE.Close();
    }

    private void CreateTableMySQL()
    {
        var connectionString = $"Server={Config.DatabaseHost};Database={Config.DatabaseName};User Id={Config.DatabaseUser};Password={Config.DatabasePassword};";

        connectionMySQL = new MySqlConnection(connectionString);
        connectionMySQL.Open();
        var query = "CREATE TABLE IF NOT EXISTS franug_viplist (steamid varchar(32) NOT NULL, time int(64) NOT NULL, PRIMARY KEY(steamid)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;";
        using (MySqlCommand command = new MySqlCommand(query,
            connectionMySQL))
        {
            command.ExecuteNonQuery();
        }

        query = "CREATE TABLE IF NOT EXISTS franug_viptest (steamid varchar(32) NOT NULL, time int(64) NOT NULL, PRIMARY KEY(steamid)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;";
        using (MySqlCommand command = new MySqlCommand(query,
            connectionMySQL))
        {
            command.ExecuteNonQuery();
        }
        connectionMySQL.Close();
    }

    private async Task<bool> RecordTestExistsSQLite(CCSPlayerController player)
    {
        try
        {
            await connectionSQLITE.OpenAsync();

            var query = "SELECT * FROM franug_viptest WHERE steamid = @steamid;";

            var command = new SqliteCommand(query, connectionSQLITE);
            command.Parameters.AddWithValue("@steamid", player.SteamID);
            var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] RecordTestExistsSQLite ******* An error occurred: {ex.Message}");
            return false;
        }
        finally
        {
            await connectionSQLITE?.CloseAsync();
        }
    }

    private async Task<bool> RecordTestAllExistsSQLite(CCSPlayerController player)
    {
        try
        {
            await connectionSQLITE.OpenAsync();

            var query = "SELECT * FROM franug_viptest WHERE time > @current;";

            var command = new SqliteCommand(query, connectionSQLITE);
            command.Parameters.AddWithValue("@current", DateTime.UtcNow.GetUnixEpoch());
            var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] RecordTestAllExistsSQLite ******* An error occurred: {ex.Message}");
            return false;
        }
        finally
        {
            await connectionSQLITE?.CloseAsync();
        }
    }

    private async Task<bool> RecordTestExistsMySQL(CCSPlayerController player)
    {
        try
        {
            await connectionMySQL.OpenAsync();

            var query = "SELECT * FROM franug_viptest WHERE steamid = @steamid;";

            var command = new MySqlCommand(query, connectionMySQL);
            command.Parameters.AddWithValue("@steamid", player.SteamID);
            var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] RecordTestExistsMySQL ******* An error occurred: {ex.Message}");
            return false;
        }
        finally
        {
            await connectionMySQL?.CloseAsync();
        }
    }

    private async Task<bool> RecordTestAllExistsMySQL(CCSPlayerController player)
    {
        try
        {
            await connectionMySQL.OpenAsync();

            var query = "SELECT * FROM franug_viptest WHERE time > @current;";

            var command = new MySqlCommand(query, connectionMySQL);
            command.Parameters.AddWithValue("@current", DateTime.UtcNow.GetUnixEpoch());
            var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] RecordTestAllExistsMySQL ******* An error occurred: {ex.Message}");
            return false;
        }
        finally
        {
            await connectionMySQL?.CloseAsync();
        }
    }

    private async Task<bool> RecordVipExistsSQLite(long steamid)
    {
        try
        {
            await connectionSQLITE.OpenAsync();

            var query = "SELECT * FROM franug_viplist WHERE steamid = @steamid;";

            var command = new SqliteCommand(query, connectionSQLITE);
            command.Parameters.AddWithValue("@steamid", steamid);
            var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] RecordTestExistsSQLite ******* An error occurred: {ex.Message}");
            return false;
        }
        finally
        {
            await connectionSQLITE?.CloseAsync();
        }
    }

    private async Task<bool> RecordVipExistsMySQL(long steamid)
    {
        try
        {
            await connectionMySQL.OpenAsync();

            var query = "SELECT * FROM franug_viplist WHERE steamid = @steamid;";

            var command = new MySqlCommand(query, connectionMySQL);
            command.Parameters.AddWithValue("@steamid", steamid);
            var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] RecordTestExistsMySQL ******* An error occurred: {ex.Message}");
            return false;
        }
        finally
        {
            await connectionMySQL?.CloseAsync();
        }
    }

    private async Task InsertTestQueryDataSQLite(int steamid)
    {
        try
        {
            await connectionSQLITE.OpenAsync();

            var query = "REPLACE INTO franug_viptest (steamid, time) VALUES (@steamid, @time);";
            var command = new SqliteCommand(query, connectionSQLITE);

            command.Parameters.AddWithValue("@steamid", steamid);
            command.Parameters.AddWithValue("@time", CalculateEndTimeInSeconds(Config.TestVipTime));
            await command.ExecuteNonQueryAsync();
            connectionSQLITE?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] InsertTestQueryDataSQLite ******* An error occurred: {ex.Message}");
        }
        finally
        {
            await connectionSQLITE?.CloseAsync();
        }
    }

    private async Task InsertTestQueryDataMySQL(int steamid)
    {
        try
        {
            await connectionMySQL.OpenAsync();

            var query = "REPLACE INTO franug_viptest (steamid, time) VALUES (@steamid, @time);";
            var command = new MySqlCommand(query, connectionMySQL);

            command.Parameters.AddWithValue("@steamid", steamid);
            command.Parameters.AddWithValue("@time", CalculateEndTimeInSeconds(Config.TestVipTime));
            await command.ExecuteNonQueryAsync();
            connectionMySQL?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] InsertTestQueryDataMySQL ******* An error occurred: {ex.Message}");
        }
        finally
        {
            await connectionMySQL?.CloseAsync();
        }
    }

    private async Task removeVipAsync(long steamid)
    {
        if (Config.DatabaseType != "MySQL")
        {
            _ = RemoveVipQueryDataSQLite(steamid);
        }
        else
        {
            _ = RemoveVipQueryDataMySQL(steamid);
        }

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot || !player.IsValid || (long)player.SteamID != steamid)
            {
                continue;

            }
            else
            {
                bVip[(int)player.Index] = false;
            }
        }
    }

    private async Task RemoveVipQueryDataSQLite(long steamid)
    {
        try
        {
            await connectionSQLITE.OpenAsync();

            var query = "DELETE FROM franug_viplist WHERE steamid = @steamid;";
            var command = new SqliteCommand(query, connectionSQLITE);

            command.Parameters.AddWithValue("@steamid", steamid);
            await command.ExecuteNonQueryAsync();
            connectionSQLITE?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] RemoveVipQueryDataSQLite ******* An error occurred: {ex.Message}");
        }
        finally
        {
            await connectionSQLITE?.CloseAsync();
        }
    }

    private async Task RemoveVipQueryDataMySQL(long steamid)
    {
        try
        {
            await connectionMySQL.OpenAsync();

            var query = "DELETE FROM franug_viplist WHERE steamid = @steamid;";
            var command = new MySqlCommand(query, connectionMySQL);

            command.Parameters.AddWithValue("@steamid", steamid);
            await command.ExecuteNonQueryAsync();
            connectionMySQL?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] RemoveVipQueryDataMySQL ******* An error occurred: {ex.Message}");
        }
        finally
        {
            await connectionMySQL?.CloseAsync();
        }
    }

    private void updateVipAsync(long steamid, int time)
    {
        if (Config.DatabaseType != "MySQL")
        {
            _ = UpdateVipQueryDataSQLite(steamid, time);
        }
        else
        {
            _ = UpdateVipQueryDataMySQL(steamid, time);
        }
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot || !player.IsValid || (long)player.SteamID != steamid)
            {
                continue;

            }
            else
            {
                player.PrintToChat("You got vip access");
                bVip[(int)player.Index] = true;
            }
        }
    }

    private async Task UpdateVipQueryDataSQLite(long steamid, int time)
    {
        try
        {
            await connectionSQLITE.OpenAsync();

            var query = "REPLACE INTO franug_viplist (steamid, time) VALUES (@steamid, @time);";
            var command = new SqliteCommand(query, connectionSQLITE);

            command.Parameters.AddWithValue("@steamid", steamid);
            command.Parameters.AddWithValue("@time", time);
            await command.ExecuteNonQueryAsync();
            connectionSQLITE?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] UpdateVipQueryDataSQLite ******* An error occurred: {ex.Message}");
        }
        finally
        {
            await connectionSQLITE?.CloseAsync();
        }
    }

    private async Task UpdateVipQueryDataMySQL(long steamid, int time)
    {
        try
        {
            await connectionMySQL.OpenAsync();

            var query = "REPLACE INTO franug_viplist (steamid, time) VALUES (@steamid, @time);";
            var command = new MySqlCommand(query, connectionMySQL);

            command.Parameters.AddWithValue("@steamid", steamid);
            command.Parameters.AddWithValue("@time", time);
            await command.ExecuteNonQueryAsync();
            connectionMySQL?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] UpdateVipQueryDataMySQL ******* An error occurred: {ex.Message}");
        }
        finally
        {
            await connectionMySQL?.CloseAsync();
        }
    }

    private void getPlayerData(CCSPlayerController player)
    {
        if (Config.DatabaseType != "MySQL")
        {
            _ = GetUserDataSQLite(player);
        }
        else
        {
            _ = GetUserDataMySQL(player);
        }
    }

    public async Task GetUserDataSQLite(CCSPlayerController player)
    {
        try
        {
            await connectionSQLITE.OpenAsync();

            var query = "SELECT * FROM franug_viplist WHERE steamid = @steamid;";

            var command = new SqliteCommand(query, connectionSQLITE);
            command.Parameters.AddWithValue("@steamid", player.SteamID);
            if (Config.DebugEnabled)
            {
                Console.WriteLine("QUERY: " + query + " con steamid "+ player.SteamID);
            }
            var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int time = Convert.ToInt32(reader["time"]);
                if (Config.DebugEnabled)
                {
                    Console.WriteLine("time es "+time);
                }
                if (time <= 0 || time > DateTime.UtcNow.GetUnixEpoch())
                {
                    bVip[(int)player.Index] = true;
                }
            }

            if (!bVip[(int)player.Index])
            {
                query = "SELECT * FROM franug_viptest WHERE steamid = @steamid;";

                command = new SqliteCommand(query, connectionSQLITE);
                command.Parameters.AddWithValue("@steamid", player.SteamID);
                if (Config.DebugEnabled) Console.WriteLine("QUERY: " + query);
                reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int time = Convert.ToInt32(reader["time"]);

                    if (time <= 0 || time > DateTime.UtcNow.GetUnixEpoch())
                    {
                        bVip[(int)player.Index] = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] GetUserDataSQLite ******* An error occurred: {ex.Message}");
        }
        finally
        {
            await connectionSQLITE?.CloseAsync();
        }
    }

    public async Task GetUserDataMySQL(CCSPlayerController player)
    {
        try
        {
            await connectionMySQL.OpenAsync();

            var query = "SELECT * FROM franug_viplist WHERE steamid = @steamid;";

            var command = new MySqlCommand(query, connectionMySQL);
            command.Parameters.AddWithValue("@steamid", player.SteamID);
            if (Config.DebugEnabled) Console.WriteLine("QUERY: " + query);
            var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int time = Convert.ToInt32(reader["time"]);

                if (time <= 0 || time > DateTime.UtcNow.GetUnixEpoch())
                {
                    bVip[(int)player.Index] = true;
                }
            }

            if (!bVip[(int)player.Index])
            {
                query = "SELECT * FROM franug_viptest WHERE steamid = @steamid;";

                command = new MySqlCommand(query, connectionMySQL);
                command.Parameters.AddWithValue("@steamid", player.SteamID);
                if (Config.DebugEnabled) Console.WriteLine("QUERY: " + query);
                reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int time = Convert.ToInt32(reader["time"]);

                    if (time <= 0 || time > DateTime.UtcNow.GetUnixEpoch())
                    {
                        bVip[(int)player.Index] = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Franug-Vip] GetUserDataMySQL ******* An error occurred: {ex.Message}");
        }
        finally
        {
            await connectionMySQL?.CloseAsync();
        }
    }

    private int CalculateEndTimeInSeconds(long time) {
        return DateTime.UtcNow.AddSeconds(time * 60).GetUnixEpoch();
    }

    private bool IsPlayerValid(CCSPlayerController? player)
    {
        return (player != null && player.IsValid && !player.IsBot && !player.IsHLTV && player.PawnIsAlive);
    }
}

public static class GetUnixTime
{
    public static int GetUnixEpoch(this DateTime dateTime)
    {
        var unixTime = dateTime.ToUniversalTime() -
                       new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return (int)unixTime.TotalSeconds;
    }
}

