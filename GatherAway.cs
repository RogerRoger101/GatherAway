using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins;

[Info("Gather Away", "Notchu/Gattaca", "1.2.5")]
[Description("Auto hit Mini-game tree-X or ore-spot")]
internal class GatherAway : CovalencePlugin
{
    private class Configuration
    {
        [JsonProperty("Debug Mode (true or false)")]
        public bool DebugMode = false;

        [JsonProperty("Automatically gather X Markers")] 
        public bool XMarkerGather = true;

        [JsonProperty("Automatically gather Ore Weak spots")] 
        public bool OreWeakSpotsGather = true;

        [JsonProperty("Allow Blacklist (true or false)")]
        public bool AllowBlacklist = true;

        [JsonProperty("Allow Whitelist (true or false)")]
        public bool AllowWhitelist = false;

        [JsonProperty("Blacklist")] 
        public List<string> Blacklist = new List<string> 
        { 
            "icepick.salvaged",
            "icepick_salvaged.entity",
            "axe_salvaged.entity",
			"hammer_salvaged.entity", 
            "jackhammer.entity",
            "chainsaw.entity"
        };

        [JsonProperty("Whitelist")] 
        public List<string> Whitelist = new List<string>();

        [JsonProperty("Give players ability to turn off plugin for themselves")]
        public bool TogglePluginByPlayer = true;
    }
    private Configuration _config;
    
    private HashSet<string> disabledPlayers = new HashSet<string>();
    private const string DataFile = "GatherAwayDisabledPlayers";
    
    private void LoadData()
    {
        disabledPlayers = Interface.Oxide.DataFileSystem.ReadObject<HashSet<string>>(DataFile) ?? new HashSet<string>();
    }

    private void SaveData()
    {
        Interface.Oxide.DataFileSystem.WriteObject(DataFile, disabledPlayers);
    }
        
    protected override void LoadConfig()
    {
        base.LoadConfig();
        try
        {
            _config = Config.ReadObject<Configuration>();
            if (_config == null) throw new Exception();
        }
        catch
        {
            PrintError("Your configuration file contains an error. Using default configuration values.");
            LoadDefaultConfig();
        }
    }
    protected override void SaveConfig() => Config.WriteObject(_config);

    protected override void LoadDefaultConfig()
    {
        _config = new Configuration();
        SaveConfig();
    }
    
    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            { "GatherAwayOn", "Now you will hit weak spots of ores and trees" },
            {"GatherAwayOff", "Now you will NOT hit weak spots of ores and trees"},
            {"NoRights", "You don't have rights to use this command." },
        }, this);
    }
    
    void Init()
    {
        if (_config.TogglePluginByPlayer)
        {
            Puts("Enabled plugin by player");
            AddCovalenceCommand("gatheraway", "ToggleGatherAway");
            LoadData(); 
        }
        if (!_config.OreWeakSpotsGather)
        {
            Unsubscribe(nameof(OnPlayerAttack));
        }
        if (!_config.XMarkerGather)
        {
            Unsubscribe(nameof(OnTreeMarkerHit));
        }
        permission.RegisterPermission("gatheraway.use", this);
        permission.RegisterPermission("gatheraway.bypass", this);
    }

    void Unload()
    {
        SaveData();
        disabledPlayers.Clear();
    }
    void OnServerSave()
    {
        SaveData();
    }
    object  OnPlayerAttack(BasePlayer player, HitInfo info)
    {
        if (info == null || info.IsProjectile()) return null;
        
        if (player == null || info.HitEntity is not OreResourceEntity ore || info.InitiatorPlayer.IsBot) return null;
		
        if (_config.DebugMode)
        {
            var held = player.GetHeldEntity();
            if (held != null)
            {
                Puts($"[GatherAway Debug] {player.displayName} ({player.UserIDString}) is hitting ore with: {held.ShortPrefabName}");
            }
            else
            {
                Puts($"[GatherAway Debug] {player.displayName} has no held entity (probably projectile or weird case)");
            }
        }
        
        if (_config.TogglePluginByPlayer && disabledPlayers.Contains(player.UserIDString)) return null;
        
        if (ore._hotSpot == null) return null;
        
        // Check if player has bypass permission - if so, skip blacklist/whitelist checks
        bool hasBypass = permission.UserHasPermission(player.UserIDString, "gatheraway.bypass");
        
        if (!hasBypass)
        {
            // Check blacklist: if enabled and tool is in blacklist, forbid the plugin
            if (_config.AllowBlacklist && IsToolBlacklisted(info))
            {
                if (_config.DebugMode)
                {
                    Puts($"[GatherAway] Tool {info.Weapon.ShortPrefabName} is BLACKLISTED - weak spot NOT applied for {player.displayName}");
                }
                return null;
            }
            
            // Check whitelist: if enabled and tool is NOT in whitelist, forbid the plugin
            if (_config.AllowWhitelist && !IsToolWhitelisted(info))
            {
                if (_config.DebugMode)
                {
                    Puts($"[GatherAway] Tool {info.Weapon.ShortPrefabName} is NOT WHITELISTED - weak spot NOT applied for {player.displayName}");
                }
                return null;
            }
        }
        else if (_config.DebugMode)
        {
            Puts($"[GatherAway] Player {player.displayName} has BYPASS permission - blacklist/whitelist ignored");
        }
        
        // Always hit weak spot when conditions are met
        HitWeakSpotOnOre(info, player.UserIDString, ore);    

        return null;
    }
    bool? OnTreeMarkerHit(TreeEntity tree, HitInfo info)
    {
        var initiator = info.InitiatorPlayer;
        
        if (initiator.IsBot) return null;
        
        if (_config.TogglePluginByPlayer && disabledPlayers.Contains(initiator.UserIDString)) return null;

        // Check if player has bypass permission - if so, skip blacklist/whitelist checks
        bool hasBypass = permission.UserHasPermission(initiator.UserIDString, "gatheraway.bypass");
        
        if (!hasBypass)
        {
            // Check blacklist: if enabled and tool is in blacklist, forbid the plugin
            if (_config.AllowBlacklist && IsToolBlacklisted(info))
            {
                if (_config.DebugMode)
                {
                    Puts($"[GatherAway] Tool {info.Weapon.ShortPrefabName} is BLACKLISTED - X marker NOT applied for {initiator.displayName}");
                }
                return null;
            }
            
            // Check whitelist: if enabled and tool is NOT in whitelist, forbid the plugin
            if (_config.AllowWhitelist && !IsToolWhitelisted(info))
            {
                if (_config.DebugMode)
                {
                    Puts($"[GatherAway] Tool {info.Weapon.ShortPrefabName} is NOT WHITELISTED - X marker NOT applied for {initiator.displayName}");
                }
                return null;
            }
        }
        else if (_config.DebugMode)
        {
            Puts($"[GatherAway] Player {initiator.displayName} has BYPASS permission - blacklist/whitelist ignored");
        }
        
        // Always allow hitting X marker when conditions are met
        if (_config.DebugMode)
        {
            Puts($"[GatherAway] X marker HIT allowed for {initiator.displayName} using {info.Weapon.ShortPrefabName}");
        }
        return true;
    }

    private bool IsToolBlacklisted(HitInfo info)
    {
        if (info?.Weapon == null) return false;
        return _config.Blacklist.Contains(info.Weapon.ShortPrefabName);
    }

    private bool IsToolWhitelisted(HitInfo info)
    {
        if (info?.Weapon == null) return false;
        return _config.Whitelist.Contains(info.Weapon.ShortPrefabName);
    }

    void HitWeakSpotOnOre(HitInfo info, string playerId, OreResourceEntity ore)
    {
        // Always hit weak spot - no permission check needed
        info.HitPositionWorld = ore._hotSpot.transform.position;
        
        // Debug output when weak spot is actually hit
        if (_config.DebugMode)
        {
            var weaponName = info.Weapon?.ShortPrefabName ?? "unknown";
            Puts($"[GatherAway] WEAK SPOT HIT applied! Player: {playerId}, Tool: {weaponName}, Ore position: {ore._hotSpot.transform.position}");
        }
    }

    void ToggleGatherAway(IPlayer player, string command, string[] args)
    {
        var userId = player.Id;
        if (_config.TogglePluginByPlayer && permission.UserHasPermission(userId, "gatheraway.use"))
        {
            if (disabledPlayers.Contains(userId))
            {
                disabledPlayers.Remove(userId);
                player.Reply(lang.GetMessage("GatherAwayOn", this));
            }
            else
            {
                disabledPlayers.Add(userId);
                player.Reply(lang.GetMessage("GatherAwayOff", this));
            }

        }
        else
        {
            player.Reply(lang.GetMessage("NoRights", this));
        }
    }
}