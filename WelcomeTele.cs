using Oxide.Core;
using UnityEngine;
using System;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("WelcomeTele", "CTS Kael", "0.3.0")]
    [Description("Teleports new players to a preset location one time, with configurable messaging, delays, and reliable first-spawn handling.")]
    public class WelcomeTele : RustPlugin
    {
        // Permissions
        private const string UsedPermission = "welcometele.used";
        private const string AdminPermission = "welcometele.admin";

        // Data
        private SavedPosition savedPos;

        // Config
        private ConfigData config;

        #region Config

        private class ConfigData
        {
            // Chat message shown to the player after teleport.
            // Placeholders: {name}, {x}, {y}, {z}
            public string TeleportMessage = "Welcome, {name}! You’ve been teleported to: {x}, {y}, {z}";

            // Whether to substitute coordinates into the message.
            public bool ShowCoordinates = true;

            // Seconds to wait after spawn before teleporting (after snapshot is finished).
            public float TeleportDelay = 1.0f;

            // Seconds to wait after teleport before forcing facing.
            public float FacingDelay = 0.15f;

            // If false, we skip the facing fix (yaw/pitch) and just teleport.
            public bool EnableFacingFix = true;
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
            Puts("[WelcomeTele] Creating default config.");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) throw new Exception("Config is null");
            }
            catch
            {
                Puts("[WelcomeTele] Invalid config detected. Regenerating with defaults.");
                LoadDefaultConfig();
            }

            // Write back to ensure new fields are added if plugin updated
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        private string FormatTeleportMessage(BasePlayer player, Vector3 pos)
        {
            string msg = config.TeleportMessage ?? string.Empty;

            // Substitute placeholders
            msg = msg.Replace("{name}", player?.displayName ?? "Traveler");

            if (config.ShowCoordinates)
            {
                msg = msg.Replace("{x}", pos.x.ToString("F1"))
                         .Replace("{y}", pos.y.ToString("F1"))
                         .Replace("{z}", pos.z.ToString("F1"));
            }
            else
            {
                // Remove coord placeholders cleanly
                msg = msg.Replace("{x}", "")
                         .Replace("{y}", "")
                         .Replace("{z}", "");
            }

            // Tidy extra whitespace after removals
            msg = Regex.Replace(msg, @"\s{2,}", " ").Trim();

            return msg;
        }

        #endregion

        #region Lifecycle / Data

        private void Init()
        {
            permission.RegisterPermission(UsedPermission, this);
            permission.RegisterPermission(AdminPermission, this);

            // Load saved teleport position if present
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
                savedPos = Interface.Oxide.DataFileSystem.ReadObject<SavedPosition>(Name);

            // Treat a zero position as "unset"
            if (savedPos != null && savedPos.IsZeroish())
                savedPos = null;
        }

        private class SavedPosition
        {
            public float x, y, z;
            public float rotY; // yaw

            public Vector3 ToVector3() => new Vector3(x, y, z);
            public Quaternion ToRotation() => Quaternion.Euler(0f, rotY, 0f);

            public bool IsZeroish()
            {
                return Mathf.Approximately(x, 0f) &&
                       Mathf.Approximately(y, 0f) &&
                       Mathf.Approximately(z, 0f);
            }

            public static SavedPosition FromPlayer(BasePlayer p)
            {
                var pos = p.transform.position;
                var yaw = p.transform.rotation.eulerAngles.y;
                return new SavedPosition { x = pos.x, y = pos.y, z = pos.z, rotY = yaw };
            }
        }

        private void SavePosition(SavedPosition pos)
        {
            savedPos = pos;
            Interface.Oxide.DataFileSystem.WriteObject(Name, savedPos);
        }

        #endregion

        #region Commands

        // /wtset — save current position + facing
        [ChatCommand("wtset")]
        private void CmdSet(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }

            var saved = SavedPosition.FromPlayer(player);
            SavePosition(saved);
            player.ChatMessage($"Welcome teleport location set: {saved.x:F1}, {saved.y:F1}, {saved.z:F1} (yaw {saved.rotY:F0}°)");
            Puts($"[WelcomeTele] Saved position {saved.x},{saved.y},{saved.z} rotY:{saved.rotY}");
        }

        // /wtwhere — show current saved position
        [ChatCommand("wtwhere")]
        private void CmdWhere(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }

            if (savedPos == null)
            {
                player.ChatMessage("No welcome-teleport location set. Use /wtset to set one.");
                return;
            }

            player.ChatMessage($"Saved location: {savedPos.x:F1}, {savedPos.y:F1}, {savedPos.z:F1} (yaw {savedPos.rotY:F0}°)");
        }

        // /wtclear — clear saved position (disables teleports until set again)
        [ChatCommand("wtclear")]
        private void CmdClear(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }

            savedPos = null;
            Interface.Oxide.DataFileSystem.WriteObject(Name, new SavedPosition()); // keep file but neutralize
            player.ChatMessage("Welcome-teleport location cleared.");
            Puts("[WelcomeTele] Cleared saved position.");
        }

        #endregion

        #region Teleport + Facing

        // Wait until the player is truly "ready" (snapshot finished, alive, connected), then run an action
        private void RunWhenReady(BasePlayer player, Action action, float poll = 0.25f, float timeout = 30f)
        {
            if (player == null) return;

            float waited = 0f;
            Timer t = null;
            t = timer.Every(Mathf.Max(0.05f, poll), () =>
            {
                if (player == null || !player.IsConnected)
                {
                    t?.Destroy();
                    return;
                }

                bool receivingSnapshot = player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot);
                bool alive = !player.IsDead();

                if (!receivingSnapshot && alive)
                {
                    t?.Destroy();
                    action?.Invoke();
                    return;
                }

                waited += poll;
                if (waited >= Mathf.Max(1f, timeout))
                {
                    // Failsafe: run anyway after timeout
                    t?.Destroy();
                    action?.Invoke();
                }
            });
        }

        private void TryTeleport(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;

            if (savedPos == null)
            {
                Puts("WelcomeTele: No saved position set. Use /wtset to set a location.");
                return;
            }

            // One-time only
            if (permission.UserHasPermission(player.UserIDString, UsedPermission))
                return;

            // Ensure we're after snapshot & fully spawned before starting our own delay
            RunWhenReady(player, () =>
            {
                timer.Once(Mathf.Max(0f, config.TeleportDelay), () =>
                {
                    if (!player.IsConnected) return;

                    var pos = savedPos.ToVector3();

                    player.EnsureDismounted();
                    player.Teleport(pos);

                    if (config.EnableFacingFix)
                    {
                        // Apply facing after a brief delay (configurable)
                        timer.Once(Mathf.Max(0f, config.FacingDelay), () =>
                        {
                            if (!player.IsConnected) return;
                            SetFacing(player, savedPos.rotY);
                        });
                    }

                    // Mark as used
                    permission.GrantUserPermission(player.UserIDString, UsedPermission, this);

                    // Configurable chat message
                    var msg = FormatTeleportMessage(player, pos);
                    if (!string.IsNullOrEmpty(msg))
                        player.ChatMessage(msg);
                });
            });
        }

        // Ensures the player's facing sticks after teleport
        private void SetFacing(BasePlayer player, float yawDegrees)
        {
            if (player == null || !player.IsConnected) return;

            var rot = Quaternion.Euler(0f, yawDegrees, 0f);

            // Server-side orientation
            player.transform.rotation = rot;
            player.eyes.rotation = rot;
            player.viewAngles = new Vector3(0f, yawDegrees, 0f);

            // Push to client & align input (prevents snap-back)
            player.SendNetworkUpdateImmediate();
            player.SendConsoleCommand("input.setyaw", yawDegrees);
            player.SendConsoleCommand("input.setpitch", 0f);
        }

        #endregion

        #region Hooks

        // Fires very early (on connect); we wait for snapshot before TP
        private void OnPlayerInit(BasePlayer player) => TryTeleport(player);

        // Fires when the player finishes spawning (good for first join)
        private void OnPlayerSpawned(BasePlayer player) => TryTeleport(player);

        // Fires after respawn (post-death)
        private void OnPlayerRespawned(BasePlayer player) => TryTeleport(player);

        // Extra safety: when a sleeper wakes (first join can pass through sleeping state)
        private void OnPlayerSleepEnded(BasePlayer player) => TryTeleport(player);

        #endregion
    }
}
