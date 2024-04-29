using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Zandronum;
using Zorcher.ProjectC.ProjectCInterface.Plugin;
using Zorcher.ProjectC.ProjectCInterface.Enums;

namespace Zorcher.ProjectC.Engine {
    //
    // MANDATORY: The plug!
    // This is an important class to the core. Every plugin must
    // have exactly 1 class that inherits from Plug. When the plugin is loaded,
    // this class is instantiated and used to receive events from the core.
    // Make sure the class is public, because only public classes can be seen
    // by the core.
    //

    public sealed class ZandronumPlug : Plug {
        // Static instance. We can't use a real static class, because ZandronumPlug must
        // be instantiated by the core, so we keep a static reference. (this technique
        // should be familiar to object-oriented programmers)
        private static ZandronumPlug _me;

        // Process property that keeps handle to the server
        private static Process _serverProc;

        // Static property to access the ZandronumPlug
        public static ZandronumPlug Me { get { return _me; } }

        // Const values
        private const int LAUNCHER_MASTER_CHALLENGE = 5660023;
        //private const short MASTER_SERVER_VERSION = 2;
        private const int LAUNCHER_SERVER_CHALLENGE = 199;
        private const int SERVER_LAUNCHER_CHALLENGE = 5660023;
        private const int SERVER_LAUNCHER_SEGMENTED_CHALLENGE = 5660031;

        // Override this property if you want to give your plugin a name other
        // than the filename without extension.
        public override string Name { get { return "Zandronum"; } }

        // Override this property if your plugin supports masterservers
        public override string ServerColumnHeaders { get { return "Name|Ping|Users|Game Type|Map|Wads"; } }

        // Override this property if your plugin uses huffman encryption
        public override bool Huffman { get { return true; } }

        // Override this property if your plugin has a UDP protocol masterserver
        public override bool UdpMaster { get { return true; } }

        // EngineFlags property that specifies what I can do with the plugin
        public override EngineFlags EngineFlag { get { return EngineFlags.MULTIPLAY | EngineFlags.JOIN | EngineFlags.SERVER; } }

        // This event is called when the plugin is initialized
        public override void OnInitialize() {
            base.OnInitialize();

            // Keep a static reference
            _me = this;
        }

        // This event is called when the plugin is setup
        public override bool OnEngineSetup() {
            // Get configured engine location
            string enginepath = Plugin.General.ReadPluginSetting( "general_enginepath", string.Empty );

            // Check if configured
            if ( enginepath.Length > 0 ) {
                // Check if engine can be found
                if ( !File.Exists( Path.Combine( enginepath, "zandronum.exe" ) ) ) {
                    // Show message, location is invalid
                    Plugin.General.ShowWarningMessage( "Zandronum.exe could not be found in the engine location you specified.\nPlease make sure you selected the right folder!", MessageBoxButtons.OK );
                    return false;
                }

                // Check if engine files can be found
                if ( !File.Exists( Path.Combine( enginepath, "zandronum.pk3" ) ) ) {
                    // Show message, location is invalid
                    Plugin.General.ShowWarningMessage( "Zandronum.pk3 could not be found in the engine location you specified.\nPlease make sure you selected the right folder!", MessageBoxButtons.OK );
                    return false;
                }

                // Get the iwad path
                string iwadpath = Plugin.General.ReadPluginSetting( "general_iwadpath", string.Empty );
                if ( iwadpath.Length == 0 ) {
                    // Show message, no iwad path was configured
                    Plugin.General.ShowWarningMessage( "Please specify a folder where you store your IWADs.", MessageBoxButtons.OK );
                    return false;
                }

                // Check IWADs in custom folders
                bool iwadfound = File.Exists( Path.Combine( iwadpath, "doom1.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "doom.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "doom2.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "plutonia.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "tnt.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "heretic.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "hexen.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "strife1.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "chex.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "chex3.wad" ) );
                iwadfound |= File.Exists( Path.Combine( iwadpath, "freedm.wad" ) );

                // Check if no IWAD could be found
                if ( !iwadfound ) {
                    // Show message, no IWAD found
                    Plugin.General.ShowWarningMessage( "No valid IWAD file could be found.\nPlease make sure you have put your IWAD files in the specified folder!", MessageBoxButtons.OK );
                    return false;
                }

                // Get the pwad path
                string wadpath = Plugin.General.ReadPluginSetting( "general_wadpath", string.Empty );
                if ( wadpath.Length == 0 ) {
                    // Show message, no wad path was configured
                    Plugin.General.ShowWarningMessage( "Please specify a folder where you store your PWADs.", MessageBoxButtons.OK );
                    return false;
                }
            } else {
                // Engine path not configured
                return false;
            }

            // All good
            return true;
        }

        // This event is called when the multiplayer is being setup
        public override bool OnMultiplayerSetup( IDictionary gameconfig ) {
            // Get configured iwad location
            string iwadpath = Plugin.General.ReadPluginSetting( "general_iwadpath", string.Empty );

            // Get the mod name
            string iwadname = (string)gameconfig["mod"];
            int index = iwadname.IndexOf( " - " );
            if ( index >= 0 ) iwadname = string.Format( "{0}.wad", iwadname.Substring( 0, index ) );

            // Get the map name
            string mapname = (string)gameconfig["map"];
            index = mapname.IndexOf( " - " );
            if ( index >= 0 ) mapname = mapname.Substring( 0, index );

            // Check if a mod is configured
            if ( iwadname.Length >= 1 ) {
                // Verify the iwad exists
                if ( !File.Exists( Path.Combine( iwadpath, iwadname ) ) ) {
                    // Show message, iwad was not found
                    Plugin.General.ShowWarningMessage( "The IWAD specified could not be found.\nPlease verify your IWAD Folder is set correctly.", MessageBoxButtons.OK );
                    return false;
                }
            } else {
                // Show message, no iwad was configured
                Plugin.General.ShowWarningMessage( "Please specify an IWAD to use in the mod dialog.", MessageBoxButtons.OK );
                return false;
            }

            // Check if a map is configured
            if ( mapname.Length < 1 ) {
                // Show message, no map was configured
                Plugin.General.ShowWarningMessage( "Please specify an existing map name.", MessageBoxButtons.OK );
                return false;
            }

            // Are we using the Strife IWAD?
            if ( iwadname == "strife1.wad" ) {
                // Verify if the additional voices wad is found
                if ( !File.Exists( Path.Combine( iwadpath, "voices.wad" ) ) ) {
                    // Show message, no voice support in Strife
                    Plugin.General.ShowWarningMessage( "You do not have the voices.wad that is a part of the game Strife.\nYou will be unable to hear chatter of the game's characters without this wad.", MessageBoxButtons.OK );
                }
            }

            // Verify pwads
            string wadpaths = Plugin.General.ReadPluginSetting( "general_wadpath", string.Empty );
            string wads = (string)gameconfig["general_wads"];
            string[] paths = wadpaths.Split( '|' );

            // Verify if we specified a wad file
            if ( wads.Length > 1 ) {
                // Search through each wad file
                foreach ( string wad in wads.Split( '|' ) ) {
                    bool filefound = false;

                    // Search through each path where the wad file could be
                    foreach ( string path in paths ) {
                        // Verify the file exists
                        if ( !File.Exists( Path.Combine( path, wad ) ) ) continue;

                        // File found, verify it
                        FileVerification( path, wad );

                        // File found, stop the search
                        filefound = true;
                        break;
                    }

                    // Verify file found
                    if ( filefound ) continue;

                    // Show message, wad not found
                    Plugin.General.ShowWarningMessage( string.Format( "The specified wad file \"{0}\" could not be found.\nPlease make sure you have put your custom files in the specified folder!", wad ), MessageBoxButtons.OK );
                    return false;
                }
            }

            return true;
        }

        // This event is called when the files are being verified
        public override void OnFileVerification( IDictionary gameconfig ) {
            // Get configured engine location
            string enginepath = Plugin.General.ReadPluginSetting( "general_enginepath", string.Empty );

            // Get configured iwad location
            string iwadpath = Plugin.General.ReadPluginSetting( "general_iwadpath", string.Empty );

            // Verify Zandronum server/client
            FileVerification( enginepath, "zandronum.exe" );

            // Verify Zandronum pk3
            FileVerification( enginepath, "zandronum.pk3" );

            // Verify IWAD file
            string iwadname = (string)gameconfig["mod"];
            int index = iwadname.IndexOf( " - " );
            if ( index >= 0 ) iwadname = iwadname.Substring( 0, index );
            FileVerification( iwadpath, string.Format( "{0}.wad", iwadname ) );

            // Verify pwads
            string wadpaths = Plugin.General.ReadPluginSetting( "general_wadpath", string.Empty );
            string wads = (string)gameconfig["general_wads"];
            string[] paths = wadpaths.Split( '|' );

            // Search through each wad file
            foreach ( string wad in wads.Split( '|' ) ) {
                // Search through each path where the wad file could be
                foreach ( string path in paths ) {
                    // Verify the file exists
                    if ( !File.Exists( Path.Combine( path, wad ) ) ) continue;

                    // File found, verify it
                    FileVerification( path, wad );

                    // File found, stop the search
                    break;
                }
            }
        }

        // This is called when the plugin is launched
        public override Process OnEngineLaunch( IDictionary gameconfig, bool online, bool host, string username, string hostaddress, int clients ) {
            // Get configured engine location
            string enginepath = Plugin.General.ReadPluginSetting( "general_enginepath", string.Empty );

            // Get configured iwad location
            string iwadpath = Plugin.General.ReadPluginSetting( "general_iwadpath", string.Empty );

            // Get configured wad locations
            string wadpaths = Plugin.General.ReadPluginSetting( "general_wadpath", string.Empty );

            // Get configured wads
            string wads = (string)gameconfig["general_wads"];

            // Get the IWAD we're using
            string iwadname = (string)gameconfig["mod"];
            int index = iwadname.IndexOf( " - " );
            if ( index >= 0 ) iwadname = iwadname.Substring( 0, index );
            string iwadfile = Path.Combine( iwadpath, string.Format( "{0}.wad", iwadname ) );

            // Go for all custom files to make a string
            string cwadfiles = "-file";

            // Search through each wad file
            foreach ( string wad in wads.Split( '|' ) ) {
                // Search through each path
                foreach ( string path in wadpaths.Split( '|' ) ) {
                    string cwadpathandfile = Path.Combine( path, wad );

                    // Verify the file exists
                    if ( !File.Exists( cwadpathandfile ) ) continue;

                    // Add file to string
                    cwadfiles += " ";
                    cwadfiles += "\"";
                    cwadfiles += cwadpathandfile;
                    cwadfiles += "\"";

                    // File found, stop the search
                    break;
                }
            }

            // Make the engine filename plus the program Zandronum.exe
            string engineFile = enginepath + "\\zandronum.exe";
            string serverFile = enginepath + "\\zandronum.exe";

            StringBuilder engineParams = new StringBuilder( 1024 );
            StringBuilder serverParams = new StringBuilder( 4096 );

            // Make the command line parameters
            engineParams.Append( "-connect " );
            engineParams.Append( hostaddress );
            serverParams.Append( "-host -iwad \"" );
            engineParams.Append( " -iwad \"" );
            serverParams.Append( iwadfile );
            engineParams.Append( iwadfile );

            serverParams.Append( "\" " );
            engineParams.Append( "\" " );

            serverParams.Append( "+sv_hostname \"Zandronum Server created by Project C\" " );
            serverParams.Append( "+sv_website \"http://www.zandronum.com/\" " );
            serverParams.Append( "+sv_hostemail \"admin@zandronum.com\" " );

            // Add custom files if any
            if ( wads.Length > 0 ) {
                serverParams.Append( cwadfiles + " " );
                engineParams.Append( cwadfiles + " " );
            }

            // Check if we should write parameters for server
            if ( host ) {
                // No call vote
                int nocallvote = Convert.ToInt32( gameconfig["voteflags_nocallvote"] );
                serverParams.Append( "+sv_nocallvote " );
                serverParams.Append( nocallvote );
                serverParams.Append( " " );

                // Vote flags
                serverParams.Append( "+sv_nomapvote " ); serverParams.Append( ( (bool)gameconfig["voteflags_nomapvote"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_nochangemapvote " ); serverParams.Append( ( (bool)gameconfig["voteflags_nochangemapvote"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_noduellimitvote " ); serverParams.Append( ( (bool)gameconfig["voteflags_noduellimitvote"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_nofraglimitvote " ); serverParams.Append( ( (bool)gameconfig["voteflags_nofraglimitvote"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_nopointlimitvote " ); serverParams.Append( ( (bool)gameconfig["voteflags_nopointlimitvote"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_notimelimitvote " ); serverParams.Append( ( (bool)gameconfig["voteflags_notimelimitvote"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_nowinlimitvote " ); serverParams.Append( ( (bool)gameconfig["voteflags_nowinlimitvote"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_nokickvote " ); serverParams.Append( ( (bool)gameconfig["voteflags_nokickvote"] ) ? "1 " : "0 " );

                // Player flags
                serverParams.Append( "+sv_nokill " ); serverParams.Append( ( (bool)gameconfig["playerflags_nokill"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_noautoaim " ); serverParams.Append( ( (bool)gameconfig["playerflags_noautoaim"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_noautomap " ); serverParams.Append( ( (bool)gameconfig["playerflags_noautomap"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_noautomapallies " ); serverParams.Append( ( (bool)gameconfig["playerflags_noautomapallies"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_disallowspying " ); serverParams.Append( ( (bool)gameconfig["playerflags_disallowspying"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_chasecam " ); serverParams.Append( ( (bool)gameconfig["playerflags_chasecam"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_norespawn " ); serverParams.Append( ( (bool)gameconfig["playerflags_norespawn"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_norocketjumping " ); serverParams.Append( ( (bool)gameconfig["playerflags_norocketjumping"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_losefrag " ); serverParams.Append( ( (bool)gameconfig["playerflags_losefrag"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_keepfrags " ); serverParams.Append( ( (bool)gameconfig["playerflags_keepfrags"] ) ? "1 " : "0 " );
                serverParams.Append( "+sv_keepteams " ); serverParams.Append( ( (bool)gameconfig["playerflags_keepteams"] ) ? "1 " : "0 " );

                // Create dmflags
                int dmflags = 0;
                if ( (bool)gameconfig["dmflags1_nohealth"] ) dmflags += (int)DMFlags1.NoHealth;
                if ( (bool)gameconfig["dmflags1_noitems"] ) dmflags += (int)DMFlags1.NoItems;
                if ( (bool)gameconfig["dmflags1_weaponstay"] ) dmflags += (int)DMFlags1.WeaponStay;
                if ( (bool)gameconfig["dmflags1_oldfalldamage"] ) dmflags += (int)DMFlags1.OldFallDamage;
                if ( (bool)gameconfig["dmflags1_hexenfallingdamage"] ) dmflags += (int)DMFlags1.HexenFallDamage;
                if ( (bool)gameconfig["dmflags1_samelevel"] ) dmflags += (int)DMFlags1.SameLevel;
                if ( (bool)gameconfig["dmflags1_spawnfarthest"] ) dmflags += (int)DMFlags1.SpawnFarthest;
                if ( (bool)gameconfig["dmflags1_forcerespawn"] ) dmflags += (int)DMFlags1.ForceRespawn;
                if ( (bool)gameconfig["dmflags1_noarmor"] ) dmflags += (int)DMFlags1.NoArmor;
                if ( (bool)gameconfig["dmflags1_noexit"] ) dmflags += (int)DMFlags1.NoExit;
                if ( (bool)gameconfig["dmflags1_infiniteammo"] ) dmflags += (int)DMFlags1.InfiniteAmmo;
                if ( (bool)gameconfig["dmflags1_nomonsters"] ) dmflags += (int)DMFlags1.NoMonsters;
                if ( (bool)gameconfig["dmflags1_monsterrespawn"] ) dmflags += (int)DMFlags1.MonsterRespawn;
                if ( (bool)gameconfig["dmflags1_itemrespawn"] ) dmflags += (int)DMFlags1.ItemRespawn;
                if ( (bool)gameconfig["dmflags1_fastmonsters"] ) dmflags += (int)DMFlags1.FastMonsters;
                if ( (bool)gameconfig["dmflags1_nojump"] ) dmflags += (int)DMFlags1.NoJumping;
                if ( (bool)gameconfig["dmflags1_nofreelook"] ) dmflags += (int)DMFlags1.NoFreeLook;
                if ( (bool)gameconfig["dmflags1_respawnsuper"] ) dmflags += (int)DMFlags1.RespawnSuper;
                if ( (bool)gameconfig["dmflags1_nofov"] ) dmflags += (int)DMFlags1.NoFOV;
                if ( (bool)gameconfig["dmflags1_noweaponspawn"] ) dmflags += (int)DMFlags1.NoWeaponSpawn;
                if ( (bool)gameconfig["dmflags1_nocrouch"] ) dmflags += (int)DMFlags1.NoCrouching;
                if ( (bool)gameconfig["dmflags1_cooploseinventory"] ) dmflags += (int)DMFlags1.CoopLoseInventory;
                if ( (bool)gameconfig["dmflags1_cooplosekeys"] ) dmflags += (int)DMFlags1.CoopLoseKeys;
                if ( (bool)gameconfig["dmflags1_cooploseweapons"] ) dmflags += (int)DMFlags1.CoopLoseWeapons;
                if ( (bool)gameconfig["dmflags1_cooplosearmor"] ) dmflags += (int)DMFlags1.CoopLoseArmor;
                if ( (bool)gameconfig["dmflags1_cooplosepowerups"] ) dmflags += (int)DMFlags1.CoopLosePowerups;
                if ( (bool)gameconfig["dmflags1_cooploseammo"] ) dmflags += (int)DMFlags1.CoopLoseAmmo;
                if ( (bool)gameconfig["dmflags1_coophalveammo"] ) dmflags += (int)DMFlags1.CoopHalfAmmo;

                // Create dmflags2
                int dmflags2 = 0;
                if ( (bool)gameconfig["dmflags2_weapondrop"] ) dmflags2 += (int)DMFlags2.WeaponDrop;
                if ( (bool)gameconfig["dmflags2_norunes"] ) dmflags2 += (int)DMFlags2.NoRunes;
                if ( (bool)gameconfig["dmflags2_instantreturn"] ) dmflags2 += (int)DMFlags2.InstantReturn;
                if ( (bool)gameconfig["dmflags2_noteamswitch"] ) dmflags2 += (int)DMFlags2.NoTeamSwitch;
                if ( (bool)gameconfig["dmflags2_noteamselect"] ) dmflags2 += (int)DMFlags2.NoTeamSelect;
                if ( (bool)gameconfig["dmflags2_doubleammo"] ) dmflags2 += (int)DMFlags2.DoubleAmmo;
                if ( (bool)gameconfig["dmflags2_degeneration"] ) dmflags2 += (int)DMFlags2.Degeneration;
                if ( (bool)gameconfig["dmflags2_bfgfreeaim"] ) dmflags2 += (int)DMFlags2.BfgFreeAim;
                if ( (bool)gameconfig["dmflags2_barrelrespawn"] ) dmflags2 += (int)DMFlags2.BarrelRespawn;
                if ( (bool)gameconfig["dmflags2_norespawninvul"] ) dmflags2 += (int)DMFlags2.NoRespawnInvulnerability;
                if ( (bool)gameconfig["dmflags2_shotgunstart"] ) dmflags2 += (int)DMFlags2.ShotgunStart;
                if ( (bool)gameconfig["dmflags2_samespawnspot"] ) dmflags2 += (int)DMFlags2.SameSpawnSpot;
                if ( (bool)gameconfig["dmflags2_sameteam"] ) dmflags2 += (int)DMFlags2.SameTeam;
                if ( (bool)gameconfig["dmflags2_forcegldefaults"] ) dmflags2 += (int)DMFlags2.ForceGlDefaults;
                if ( (bool)gameconfig["dmflags2_awarddamageinsteadkills"] ) dmflags2 += (int)DMFlags2.AwardDamageInsteadOfKills;
                if ( (bool)gameconfig["dmflags2_coopspactorspawn"] ) dmflags2 += (int)DMFlags2.CoopSpActorSpawn;

                // Create compatflags
                int compatflags = 0;
                if ( (bool)gameconfig["compatflags_shorttex"] ) compatflags += (int)CompatFlags1.ShortTextures;
                if ( (bool)gameconfig["compatflags_stairs"] ) compatflags += (int)CompatFlags1.Stairs;
                if ( (bool)gameconfig["compatflags_limitpain"] ) compatflags += (int)CompatFlags1.LimitPain;
                if ( (bool)gameconfig["compatflags_silentpickup"] ) compatflags += (int)CompatFlags1.SilentPickup;
                if ( (bool)gameconfig["compatflags_nopassover"] ) compatflags += (int)CompatFlags1.NoPassOver;
                if ( (bool)gameconfig["compatflags_soundslots"] ) compatflags += (int)CompatFlags1.SoundSlots;
                if ( (bool)gameconfig["compatflags_wallrun"] ) compatflags += (int)CompatFlags1.WallRun;
                if ( (bool)gameconfig["compatflags_notossdrops"] ) compatflags += (int)CompatFlags1.NoTossDrops;
                if ( (bool)gameconfig["compatflags_useblocking"] ) compatflags += (int)CompatFlags1.UseBlocking;
                if ( (bool)gameconfig["compatflags_nodoorlight"] ) compatflags += (int)CompatFlags1.NoDoorLight;
                if ( (bool)gameconfig["compatflags_ravenscroll"] ) compatflags += (int)CompatFlags1.RavenScroll;
                if ( (bool)gameconfig["compatflags_sectorsound"] ) compatflags += (int)CompatFlags1.SectorSound;
                if ( (bool)gameconfig["compatflags_dehhealth"] ) compatflags += (int)CompatFlags1.DehHealth;
                if ( (bool)gameconfig["compatflags_trace"] ) compatflags += (int)CompatFlags1.Trace;
                if ( (bool)gameconfig["compatflags_dropoff"] ) compatflags += (int)CompatFlags1.Dropoff;
                if ( (bool)gameconfig["compatflags_boomscroll"] ) compatflags += (int)CompatFlags1.BoomScroll;
                if ( (bool)gameconfig["compatflags_invisibility"] ) compatflags += (int)CompatFlags1.Invisiblity;
                if ( (bool)gameconfig["compatflags_limitedairmovement"] ) compatflags += (int)CompatFlags1.LimitedAirMovement;
                if ( (bool)gameconfig["compatflags_plasmabump"] ) compatflags += (int)CompatFlags1.PlasmaBumpBug;
                if ( (bool)gameconfig["compatflags_instantrespawn"] ) compatflags += (int)CompatFlags1.InstantRespawn;
                if ( (bool)gameconfig["compatflags_disabletaunts"] ) compatflags += (int)CompatFlags1.DisableTaunts;
                if ( (bool)gameconfig["compatflags_originalsoundcurve"] ) compatflags += (int)CompatFlags1.OriginalSoundCurve;
                if ( (bool)gameconfig["compatflags_oldintermission"] ) compatflags += (int)CompatFlags1.OldIntermission;
                if ( (bool)gameconfig["compatflags_disablestealthmonsters"] ) compatflags += (int)CompatFlags1.DisableStealthMonsters;
                if ( (bool)gameconfig["compatflags_oldradiusdmg"] ) compatflags += (int)CompatFlags1.OldRadiusDMG;
                if ( (bool)gameconfig["compatflags_nocrosshair"] ) compatflags += (int)CompatFlags1.NoCrossHair;
                if ( (bool)gameconfig["compatflags_oldweaponswitch"] ) compatflags += (int)CompatFlags1.OldWeaponSwitch;

                // Create compatflags2
                int compatflags2 = 0;
                if ( (bool)gameconfig["compatflags2_netscriptsareclientside"] ) compatflags2 += (int)CompatFlags2.NetScriptsClientSide;
                if ( (bool)gameconfig["compatflags2_clientssendfullbuttoninfo"] ) compatflags2 += (int)CompatFlags2.ClientsSendFullButtonInfo;
                if ( (bool)gameconfig["compatflags2_noland"] ) compatflags2 += (int)CompatFlags2.NoLand;

                // Create lmsflags
                int lmsflags = 0;
                if ( (bool)gameconfig["lmsflags_allowpistol"] ) lmsflags += (int)LMSFlags.Pistol;
                if ( (bool)gameconfig["lmsflags_allowshotgun"] ) lmsflags += (int)LMSFlags.Shotgun;
                if ( (bool)gameconfig["lmsflags_allowssg"] ) lmsflags += (int)LMSFlags.SuperShotgun;
                if ( (bool)gameconfig["lmsflags_allowchaingun"] ) lmsflags += (int)LMSFlags.Chaingun;
                if ( (bool)gameconfig["lmsflags_allowminigun"] ) lmsflags += (int)LMSFlags.Minigun;
                if ( (bool)gameconfig["lmsflags_allowrocketlauncher"] ) lmsflags += (int)LMSFlags.RocketLauncher;
                if ( (bool)gameconfig["lmsflags_allowgrenadelauncher"] ) lmsflags += (int)LMSFlags.GrenadeLauncher;
                if ( (bool)gameconfig["lmsflags_allowplasma"] ) lmsflags += (int)LMSFlags.Plasma;
                if ( (bool)gameconfig["lmsflags_allowrailgun"] ) lmsflags += (int)LMSFlags.Railgun;
                if ( (bool)gameconfig["lmsflags_allowchainsaw"] ) lmsflags += (int)LMSFlags.Chainsaw;

                // Set cheats
                if ( (bool)gameconfig["settings_cheats"] ) serverParams.Append( "+sv_cheats 1 " );

                // Set broadcast to masterserver
                serverParams.Append( "+sv_updatemaster " );
                serverParams.Append( ( (bool)gameconfig["settings_broadcast"] ) ? "1 " : "0 " );

                // Set server logging
                if ( (bool)gameconfig["settings_servlog"] ) {
                    string logpath = (string)gameconfig["settings_logpath"];
                    serverParams.Append( "+logfile " );
                    serverParams.Append( "\"" );
                    serverParams.Append( logpath );
                    serverParams.Append( "\" " );
                }

                // Set timestamp on logging
                if ( (bool)gameconfig["settings_timestamp"] ) {
                    serverParams.Append( "+sv_timestamp 1 " );
                    int timestampformat = Convert.ToInt32( gameconfig["settings_timestampformat"] );
                    serverParams.Append( "+sv_timestampformat " + timestampformat + " " );
                }

                // Get map name
                string mapname = (string)gameconfig["map"];
                mapname = mapname.ToUpper();

                // Find the space in map name 
                int pos = mapname.IndexOf( ' ' );

                // Get the mapname
                if ( pos >= 0 ) mapname = mapname.Substring( 0, pos );

                // Add map warp
                serverParams.Append( "+map " + mapname + " " );

                // Game Mode
                switch ( Convert.ToInt32( gameconfig["general_type"] ) ) {
                    case 0: serverParams.Append( "+deathmatch 0 " ); break;
                    case 1: serverParams.Append( "+survival 1 " ); break;
                    case 2: serverParams.Append( "+invasion 1 " ); break;
                    case 3: serverParams.Append( "+deathmatch 1 " ); break;
                    case 4: serverParams.Append( "+teamplay 1 " ); break;
                    case 5: serverParams.Append( "+duel 1 " ); break;
                    case 6: serverParams.Append( "+terminator 1 " ); break;
                    case 7: serverParams.Append( "+lastmanstanding 1 " ); break;
                    case 8: serverParams.Append( "+teamlms 1 " ); break;
                    case 9: serverParams.Append( "+possession 1 " ); break;
                    case 10: serverParams.Append( "+teampossession 1 " ); break;
                    case 11: serverParams.Append( "+teamgame 1 " ); break;
                    case 12: serverParams.Append( "+ctf 1 " ); break;
                    case 13: serverParams.Append( "+oneflagctf 1 " ); break;
                    case 14: serverParams.Append( "+skulltag 1 " ); break;
                }

                // Game Mode Modifier
                switch ( Convert.ToInt32( gameconfig["general_modifier"] ) ) {
                    case 1: serverParams.Append( "+buckshot 1 " ); break;
                    case 2: serverParams.Append( "+instagib 1 " ); break;
                }

                // Skill
                int skill = Convert.ToInt32( gameconfig["general_skill"] );
                serverParams.Append( "+skill " + skill );

                // Add dmflags calculated earlier
                serverParams.Append( " +dmflags " + dmflags );
                serverParams.Append( " +dmflags2 " + dmflags2 );

                // Add compatflags calculated earlier
                serverParams.Append( " +compatflags " + compatflags );
                serverParams.Append( " +compatflags2 " + compatflags2 );

                // Add lmsflags calculated earlier
                serverParams.Append( " +lmsallowedweapons " + lmsflags );

                // Set LMS spectator flags
                serverParams.Append( " +lms_spectatorchat " ); serverParams.Append( ( (bool)gameconfig["lmsflags_spectatorchat"] ) ? "1 " : "0 " );
                serverParams.Append( "+lms_spectatorview " ); serverParams.Append( ( (bool)gameconfig["lmsflags_spectatorview"] ) ? "1 " : "0 " );

                // Set fraglimit
                int fraglimit = Convert.ToInt32( gameconfig["general_fraglimit"] );
                if ( fraglimit > 0 )
                    serverParams.AppendFormat( "+fraglimit {0} ", fraglimit );

                // Set timelimit
                int timelimit = Convert.ToInt32( gameconfig["general_timelimit"] );
                if ( timelimit > 0 )
                    serverParams.AppendFormat( "+timelimit {0} ", timelimit );

                // Set winlimit
                int winlimit = Convert.ToInt32( gameconfig["general_winlimit"] );
                if ( winlimit > 0 )
                    serverParams.AppendFormat( "+winlimit {0} ", winlimit );

                // Set pointlimit
                int pointlimit = Convert.ToInt32( gameconfig["general_pointlimit"] );
                if ( pointlimit > 0 )
                    serverParams.AppendFormat( "+pointlimit {0} ", pointlimit );

                // Set maxlives
                int maxlives = Convert.ToInt32( gameconfig["general_maxlives"] );
                if ( maxlives > 0 )
                    serverParams.AppendFormat( "+sv_maxlives {0} ", maxlives );

                // Set gravity
                int gravity = Convert.ToInt32( gameconfig["general_gravity"] );
                if ( gravity != 100 ) {
                    gravity *= 10;
                    serverParams.AppendFormat( "+sv_gravity {0} ", gravity );
                }

                // Set monster kill
                int killprecentage = Convert.ToInt32( gameconfig["general_killallmonsterspercentage"] );
                if ( killprecentage > 0 )
                    serverParams.AppendFormat( "+sv_killallmonsters_percentage {0} ", killprecentage );

                // Set teamdamage
                int teamdamage = Convert.ToInt32( gameconfig["general_teamdamage"] );
                if ( teamdamage > 0 ) {
                    string strdmg = teamdamage.ToString();
                    switch ( strdmg.Length ) {
                        case 1:
                            strdmg = strdmg.Insert( 0, "0.0" );
                            break;
                        case 2:
                            strdmg = strdmg.Insert( 0, "0." );
                            break;
                        default:
                            if ( strdmg.Length > 2 ) strdmg = strdmg.Insert( strdmg.Length - 2, "." );
                            break;
                    }

                    serverParams.AppendFormat( "+teamdamage {0} ", strdmg );
                }

                // Set additional
                //				serverParams.Append("+sv_queryignoretime 0 ");
            }

            // Set name
            if ( !string.IsNullOrEmpty( username ) )
                if ( Plugin.General.ReadPluginSetting( "general_username", false ) )
                    engineParams.Append( string.Format( "+name \"{0}\" ", username ) );

            // Engine settings
            string parameters = Plugin.General.ReadPluginSetting( "general_parameters", string.Empty );
            engineParams.AppendFormat( "{0} ", parameters );

            // Server settings
            string serverparameters = Plugin.General.ReadPluginSetting( "general_serverparameters", string.Empty );
            serverParams.AppendFormat( "{0} ", serverparameters );

            // Check if we should launch a server
            if ( host ) {
                // Make the launch structure for server
                ProcessStartInfo serverstart = new ProcessStartInfo( serverFile, serverParams.ToString() ) {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    WorkingDirectory = enginepath
                };

                // Launch the server
                _serverProc = Process.Start( serverstart );

                // Set a higher priority
                _serverProc.PriorityClass = ProcessPriorityClass.AboveNormal;

                // Wait for server
                Thread.Sleep( 1000 );
            }

            // Make the launch structure for engine
            ProcessStartInfo clientstart = new ProcessStartInfo( engineFile, engineParams.ToString() ) {
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = false,
                WorkingDirectory = enginepath
            };

            // Launch the engine
            return Process.Start( clientstart );
        }

        // This is called when the game has returned
        public override void OnMultiplayerFinish( IDictionary gameconfig ) {
            // Terminate server if running
            // This is ugly, but we have to.
            if ( _serverProc != null && !_serverProc.HasExited ) _serverProc.Kill();

            // Cleanup
            _serverProc = null;
        }

        // Return the address and port of the masterserver
        public override void OnMasterQueryInit( out string address, out int port ) {
            // Defaults
            port = 15300;

            // Do we not want to query servers?
            if ( !Plugin.General.ReadPluginSetting( "servers_performquery", true ) ) {
                // Set as empty and return nothing
                address = string.Empty;
                return;
            }

            // Get the server address
            address = Plugin.General.ReadPluginSetting( "servers_masteraddress", "master.zandronum.com" );

            // Find a colon if necessary
            int index = address.IndexOf( ':' );
            if ( index < 0 ) return;

            // Parse out the port number from the host address
            if ( !int.TryParse( address.Substring( index ), out port ) ) port = 15300;
            address = address.Substring( 0, index );
        }

        // This is called to query the masterserver
        public override byte[] OnMasterQuery() {
            // Create a Stream to hold our byte data
            using ( MemoryStream ms = new MemoryStream() ) {
                // Write to the Stream in binary
                using ( BinaryWriter bw = new BinaryWriter( ms ) ) {
                    // Write the launcher master challenge and server version number
                    bw.Write( LAUNCHER_MASTER_CHALLENGE );
                    bw.Close();
                    return ms.ToArray();
                }
            }
        }

        // This is called to interpret the masterserver response
        public override string[] OnMasterResponse( ref byte[] mdata ) {
            StringBuilder sb = new StringBuilder();

            using ( MemoryStream ms = new MemoryStream( mdata ) ) {
                using ( BinaryReader br = new BinaryReader( ms, Encoding.Default ) ) {
                    // Get the challenge
                    int challenge = ( br.BaseStream.Position + 4 < br.BaseStream.Length ) ? br.ReadInt32() : int.MaxValue;
                    bool validResponse = false;

                    // Verify challenge
                    switch ( challenge ) {
                        case 0:
                            validResponse = true;

                            break;
                        case 3:
                            Plugin.General.LobbyChatWriteLine( "You are currently banned from the Zandronum masterserver", CHATCOLOR.WARNING, false, true );
                            break;
                        case 4:
                            Plugin.General.LobbyChatWriteLine( "You are retrieving the Zandronum masterlist too quickly (click Refresh to retry after at least 10 seconds)", CHATCOLOR.WARNING, false, true );
                            break;
                        //case 6:
                        //    validResponse = true;

                        //    // Start of server query
                        //    br.ReadByte(); // Skip packet number
                        //    br.ReadByte(); // Skip Beginning of server block (MSC_SERVERBLOCK) (should be 8)
                        //    break;
                        default:
                            Plugin.General.LobbyChatWriteLine( string.Format( "Unknown error {0} received from Zandronum masterserver (click Refresh to retry)", challenge ), CHATCOLOR.WARNING, false, true );
                            break;
                    }

                    if ( validResponse ) {
                        // Keep reading the stream until all IP addresses are read
                        byte stream;
                        while ( ( stream = br.ReadByte() ) > 0 && ( br.BaseStream.Position + 6 ) < br.BaseStream.Length ) {
                            // Get address and port
                            sb.Append( br.ReadByte() ); // IP #1
                            sb.Append( '.' );
                            sb.Append( br.ReadByte() ); // IP #2
                            sb.Append( '.' );
                            sb.Append( br.ReadByte() ); // IP #3
                            sb.Append( '.' );
                            sb.Append( br.ReadByte() ); // IP #4
                            sb.Append( ':' );
                            sb.Append( Math.Abs( br.ReadInt16() ) ); // Port number (between 0 and 65535)
                            sb.Append( '|' );
                        }
                    }

                    br.Close();

                    return sb.ToString().Split( '|' );
                }
            }
        }

        // This is called when we want to query an individual server
        public override byte[] OnServerQuery() {
            using ( MemoryStream ms = new MemoryStream() ) {
                using ( BinaryWriter bw = new BinaryWriter( ms, Encoding.Default ) ) {
                    // Set the data and send what server details we want to receive
                    bw.Write( LAUNCHER_SERVER_CHALLENGE );
                    bw.Write( (int)( ServerQueryFlags.Name | ServerQueryFlags.Url | ServerQueryFlags.Email | ServerQueryFlags.MapName | ServerQueryFlags.PWads | ServerQueryFlags.GameType | ServerQueryFlags.IWad | ServerQueryFlags.ForcePassword | ServerQueryFlags.ForceJoinPassword | ServerQueryFlags.GameSkill | ServerQueryFlags.DMFlags | ServerQueryFlags.Limits | ServerQueryFlags.TeamDamage | ServerQueryFlags.NumPlayers | ServerQueryFlags.PlayerData | ServerQueryFlags.DataMD5Sum ) );
                    bw.Write( Environment.TickCount & int.MaxValue );
                    bw.Close();

                    return ms.ToArray();
                }
            }
        }

        // This is called to interpret the game server response
        public override ListViewItem OnServerResponse( ref byte[] mdata ) {
            ListViewItem lvi = new ListViewItem();

            using ( MemoryStream ms = new MemoryStream( mdata ) ) {
                using ( BinaryReader br = new BinaryReader( ms, Encoding.Default ) ) {
                    byte bValue;
                    short sValue;
                    int iValue;
                    float fValue;
                    string strValue;

                    // Read the challenge
                    int challenge;
                    if ( !Helper.ReadIntFromBinaryReader( Plugin, br, "Server challenge", out challenge ) ) return null;

                    // What is the challenge?
                    if ( challenge == SERVER_LAUNCHER_CHALLENGE ) {
                        // Get ping measure
                        int ping;
                        if ( !Helper.ReadIntFromBinaryReader( Plugin, br, "Server ping", out ping ) ) return null;

                        // Skip Version Number
                        if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Version number", out strValue ) ) return null;

                        // Read in the bits
                        int iBits;
                        if ( !Helper.ReadIntFromBinaryReader( Plugin, br, "Server Flags", out iBits ) ) return null;

                        // Get the server title
                        string title = string.Empty;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.Name ) > 0 )
                            if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Server Title", out title ) ) return null;
                        lvi.Text = title;

                        // Add the ping
                        ping = ( Environment.TickCount & int.MaxValue ) - ping;
                        string pingString = ( ping < 0 ) ? "---" : ( ping > 999 ) ? "999" : ping.ToString();
                        lvi.SubItems.Add( string.Format( "{0}ms", pingString ) );

                        // Get website
                        //						string website = string.Empty;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.Url ) > 0 )
                            if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Server Website", out strValue ) ) return null;

                        // Ignore email
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.Email ) > 0 )
                            if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Server Email", out strValue ) ) return null;

                        // Get the mapname
                        string mapname = string.Empty;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.MapName ) > 0 )
                            if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Mapname", out mapname ) ) return null;
                        mapname = mapname.ToUpper();

                        // Get the max clients
                        string clients = "?";
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.MaxClients ) > 0 ) {
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Max Clients", out bValue ) ) return null;
                            clients = bValue.ToString();
                        }

                        // Get the max players
                        string players = "?";
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.MaxPlayers ) > 0 ) {
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Max Players", out bValue ) ) return null;
                            players = bValue.ToString();
                        }

                        // Get the pwad's
                        string pwadnames = string.Empty;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.PWads ) > 0 ) {
                            byte pwads;
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "PWAD Amount", out pwads ) ) return null;

                            // Go for all pwad's
                            for ( byte p = 0; p < pwads; p++ ) {
                                // Add comma only if not the first pwad
                                if ( p > 0 ) pwadnames += ", ";
                                if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "PWAD", out strValue ) ) return null;
                                pwadnames += strValue;
                            }

                            pwadnames = pwadnames.ToLower();
                        }

                        // Get the game type
                        string gameType = string.Empty;
                        byte type = 0;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.GameType ) > 0 ) {
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Game Type", out type ) ) return null;
                            switch ( type ) {
                                case 0: gameType = "Cooperative"; break;
                                case 1: gameType = "Survival"; break;
                                case 2: gameType = "Invasion"; break;
                                case 3: gameType = "Deathmatch"; break;
                                case 4: gameType = "Teamplay"; break;
                                case 5: gameType = "Duel"; break;
                                case 6: gameType = "Terminator"; break;
                                case 7: gameType = "Last Man Standing"; break;
                                case 8: gameType = "Team LMS"; break;
                                case 9: gameType = "Possession"; break;
                                case 10: gameType = "Team Possession"; break;
                                case 11: gameType = "Team Game"; break;
                                case 12: gameType = "CTF"; break;
                                case 13: gameType = "One Flag CTF"; break;
                                case 14: gameType = "Skulltag"; break;
                                default: gameType = "Unknown"; break;
                            }

                            // Instagib
                            byte instagib;
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Instagib", out instagib ) ) return null;
                            if ( instagib != 0 ) gameType += " - Instagib";

                            // Buckshot
                            byte buckshot;
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Buckshot", out buckshot ) ) return null;
                            if ( buckshot != 0 ) gameType += " - Buckshot";
                        } else {
                            gameType = "Unknown";
                        }

                        // Discard game name
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.GameName ) > 0 )
                            if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Game Name", out strValue ) ) return null;

                        // Set the game name by iwad
                        string iwadName = string.Empty;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.IWad ) > 0 ) {
                            // Get the IWAD
                            string iwad;
                            if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "IWAD", out iwad ) ) return null;
                            iwad = iwad.ToLower();

                            // Check what game is being played)
                            switch ( iwad ) {
                                case "doom1.wad":
                                case "doom.wad":
                                    iwadName = "Doom ";
                                    break;
                                case "doom2.wad":
                                    iwadName = "Doom 2 ";
                                    break;
                                case "tnt.wad":
                                    iwadName = "Evilution ";
                                    break;
                                case "plutonia.wad":
                                    iwadName = "Plutonia ";
                                    break;
                                case "heretic.wad":
                                    iwadName = "Heretic ";
                                    break;
                                case "hexen.wad":
                                    iwadName = "Hexen ";
                                    break;
                                case "freedm.wad":
                                    iwadName = "FreeDM ";
                                    break;
                                case "strife1.wad":
                                    iwadName = "Strife ";
                                    break;
                                case "chex.wad":
                                    iwadName = "Chex Quest ";
                                    break;
                                case "chex3.wad":
                                    iwadName = "Chex Quest 3 ";
                                    break;
                                default:
                                    iwadName = string.Format( "({0}) ", iwad );
                                    break;
                            }
                        }

                        // TODO: Utilize this value
                        // Check Force Password
                        byte serverPassword;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.ForcePassword ) > 0 )
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Server Password?", out serverPassword ) ) return null;

                        // Check Join Password
                        byte serverJoinPassword;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.ForceJoinPassword ) > 0 )
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Server Join Password?", out serverJoinPassword ) ) return null;

                        // Ignore the skill
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.GameSkill ) > 0 )
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Skill", out bValue ) ) return null;

                        // Ignore bot skill
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.BotSkill ) > 0 )
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Bot skill", out bValue ) ) return null;

                        // Set the dmflags
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.DMFlags ) > 0 ) {
                            // Skip dmflags
                            if ( !Helper.ReadIntFromBinaryReader( Plugin, br, "Deathmatch flags", out iValue ) ) return null;

                            // Skip dmflags2
                            if ( !Helper.ReadIntFromBinaryReader( Plugin, br, "Deathmatch flags 2", out iValue ) ) return null;

                            // Skip compatflags
                            if ( !Helper.ReadIntFromBinaryReader( Plugin, br, "Compatibility flags", out iValue ) ) return null;
                        }

                        // Skip the limits
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.Limits ) > 0 ) {
                            // Skip fraglimit
                            if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Fraglimit", out sValue ) ) return null;

                            // Skip timelimit
                            short timelimit;
                            if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Timelimit", out timelimit ) ) return null;

                            // Check if time left is added
                            if ( timelimit > 0 ) {
                                // Skip time left
                                if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Time left", out sValue ) ) return null;
                            }

                            // Skip duellimit
                            if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Duellimit", out sValue ) ) return null;

                            // Skip pointlimit
                            if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Pointlimit", out sValue ) ) return null;

                            // Skip winlimit
                            if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Winlimit", out sValue ) ) return null;
                        }

                        // Skip teamdamage
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamDamage ) > 0 )
                            if ( !Helper.ReadSingleFromBinaryReader( Plugin, br, "Time left", out fValue ) ) return null;

                        // Skip teamscores
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamScores ) > 0 ) {
                            // Skip blue team and red team (2 bytes each)
                            if ( !Helper.ReadIntFromBinaryReader( Plugin, br, "Team Scores", out iValue ) ) return null;
                        }

                        // Get number of players
                        byte numplayers = 0;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.NumPlayers ) > 0 )
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Number of Players", out numplayers ) ) return null;

                        // Check if there are any players
                        //						string players = string.Empty;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.PlayerData ) > 0 ) {
                            // Go for all players
                            for ( byte i = 0; i < numplayers; i++ ) {
                                //								if(i > 0) players += ", ";

                                // Get player name
                                if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Player name", out strValue ) ) return null;
                                //								players += strValue;

                                // Discard player frags
                                if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Player Frags", out sValue ) ) return null;

                                // Discard player ping
                                if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Player Ping", out sValue ) ) return null;

                                // Discard player spectating
                                if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Player Spectate", out bValue ) ) return null;

                                // Discard player bot
                                if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Player Bot", out bValue ) ) return null;

                                // Discard team
                                if ( type == 4 || type == 8 || type >= 10 ) // 4, 8, 10, 11, 12, 13, 14, 15 
                                    if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Player Team", out bValue ) ) return null;

                                // Discard time
                                if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Player Time", out bValue ) ) return null;
                            }

                            //							players = Helper.StripColorCodes(players);
                        }

                        // Get number of teams
                        byte numTeams = 0;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamInfoNumber ) > 0 )
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Number of Teams", out numTeams ) ) return null;

                        // Go for all teams
                        for ( byte i = 0; i < numTeams; i++ ) {
                            // Discard team name
                            if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamInfoName ) > 0 )
                                if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Team Name", out strValue ) ) return null;

                            // Discard team color
                            if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamInfoColor ) > 0 )
                                if ( !Helper.ReadIntFromBinaryReader( Plugin, br, "Team Color", out iValue ) ) return null;

                            // Discard team score
                            if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamInfoScore ) > 0 )
                                if ( !Helper.ReadShortFromBinaryReader( Plugin, br, "Team Score", out sValue ) ) return null;
                        }

                        // Get debug build information
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TestingServer ) > 0 ) {
                            // Discard test server
                            if ( !Helper.ReadByteFromBinaryReader( Plugin, br, "Test Server?", out bValue ) ) return null;

                            // Discard test server information
                            if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "Test Server Info", out strValue ) ) return null;
                        }

                        // Discard md5 hash (32 chars)
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.DataMD5Sum ) > 0 )
                            if ( !Helper.ReadNullTerminatedStringFromBinaryReader( br, "MD5 Hash", out strValue ) ) return null;

                        // Time to add to the ListView
                        lvi.SubItems.Add( string.Format( "{0} / {1} / {2}", numplayers, players, clients ) );
                        //						lvi.SubItems.Add(website);
                        lvi.SubItems.Add( string.Format( "{0}{1}", iwadName, gameType ) );
                        lvi.SubItems.Add( mapname );
                        lvi.SubItems.Add( pwadnames );
                        //						lvi.SubItems.Add(players);

                        // Close
                        br.Close();
                    } else if ( challenge == 5660024 ) {
                        // Simply ignore
                        Plugin.General.LobbyChatWriteLine( "This server is complaining that you are pinging it too fast", CHATCOLOR.WARNING, false, true );
                        return null;
                    } else if ( challenge == 5660025 ) {
                        Plugin.General.LobbyChatWriteLine( "You are currently banned from this Zandronum server", CHATCOLOR.WARNING, false, true );
                        return null;
                    } else {
                        Plugin.General.LobbyChatWriteLine( string.Format("Unknown error received from Zandronum server (challenge: {0})", challenge ), CHATCOLOR.WARNING, false, true );
                        return null;
                    }
                }
            }

            return lvi;
        }

        // This is called when we want to display the server data
        // Data has been verified, display the server
        public override IDictionary OnServerDisplay( ref byte[] sdata ) {
            Hashtable ht = new Hashtable();
            StringBuilder sbMissingFiles = new StringBuilder();

            using ( MemoryStream ms = new MemoryStream( sdata ) ) {
                using ( BinaryReader br = new BinaryReader( ms, Encoding.Default ) ) {
                    // Read the challenge
                    int challenge = br.ReadInt32();

                    // What is the challenge?
                    if ( challenge == LAUNCHER_SERVER_CHALLENGE ) {
                        // Skip ping measure
                        br.ReadInt32();

                        // Skip Version Number
                        string strValue;
                        Helper.ReadNullTerminatedStringFromBinaryReader( br, "Version Number", out strValue );

                        // Read in the bits
                        int iBits = br.ReadInt32();

                        // Skip the server title
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.Name ) > 0 )
                            Helper.ReadNullTerminatedStringFromBinaryReader( br, "Server Title", out strValue );

                        // Add website
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.Url ) > 0 ) {
                            Helper.ReadNullTerminatedStringFromBinaryReader( br, "Server Website", out strValue );
                            ht.Add( "website", strValue );
                        }

                        // Ignore email
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.Email ) > 0 )
                            Helper.ReadNullTerminatedStringFromBinaryReader( br, "Server Email", out strValue );

                        // Get the mapname
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.MapName ) > 0 ) {
                            Helper.ReadNullTerminatedStringFromBinaryReader( br, "Server Mapname", out strValue );
                            ht.Add( "map", strValue );
                        }

                        // Skip the max clients
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.MaxClients ) > 0 )
                            br.ReadByte();

                        // Skip the max players
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.MaxPlayers ) > 0 )
                            br.ReadByte();

                        // Get the pwad's
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.PWads ) > 0 ) {
                            string wadpaths = Plugin.General.ReadPluginSetting( "general_wadpath", string.Empty );
                            string[] paths = wadpaths.Split( '|' );

                            StringBuilder pwadnames = new StringBuilder();
                            byte pwads = br.ReadByte();

                            // Go for all pwad's
                            for ( byte p = 0; p < pwads; p++ ) {
                                // Add comma only if not the first pwad
                                if ( p > 0 ) pwadnames.Append( '|' );
                                Helper.ReadNullTerminatedStringFromBinaryReader( br, "PWAD Name", out strValue );

                                bool wadFound = false;

                                foreach ( string path in paths ) {
                                    string wad = Path.Combine( path, strValue );
                                    if ( !File.Exists( wad ) ) continue;

                                    wadFound = true;
                                    break;
                                }

                                // Add wads
                                if ( !wadFound ) sbMissingFiles.Append( strValue + "|" );
                                pwadnames.Append( strValue );
                            }

                            ht.Add( "general_wads", pwadnames.ToString().ToLower() );
                        }

                        // Get the game type
                        byte type = 0;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.GameType ) > 0 ) {
                            type = br.ReadByte();
                            ht.Add( "general_type", (int)type );

                            // Get Instagib
                            byte instagib = br.ReadByte();

                            // Get Buckshot
                            byte buckshot = br.ReadByte();

                            if ( buckshot != 0 ) ht.Add( "general_modifier", 1 );
                            else if ( instagib != 0 ) ht.Add( "general_modifier", 2 );
                        }

                        // Discard game name
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.GameName ) > 0 )
                            Helper.ReadNullTerminatedStringFromBinaryReader( br, "Server Title", out strValue );

                        // Set the game name by iwad
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.IWad ) > 0 ) {
                            // Get configured iwad location
                            string iwadpath = Plugin.General.ReadPluginSetting( "general_iwadpath", string.Empty );

                            // Get the IWAD
                            string iwad; Helper.ReadNullTerminatedStringFromBinaryReader( br, "IWAD", out iwad );
                            int i = iwad.LastIndexOf( '.' ); iwad = iwad.ToLower();
                            ht.Add( "mod", ( i >= 0 ) ? iwad.Substring( 0, i ) : iwad );

                            // Verify we have the iwad
                            string iwadFile = Path.Combine( iwadpath, iwad );
                            if ( !File.Exists( iwadFile ) ) sbMissingFiles.Append( iwad + "|" );
                        }

                        // Skip Force Password
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.ForcePassword ) > 0 )
                            br.ReadByte();

                        // Skip Join Password
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.ForceJoinPassword ) > 0 )
                            br.ReadByte();

                        // Set the skill
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.GameSkill ) > 0 ) {
                            byte skill = br.ReadByte();
                            ht.Add( "general_skill", (int)skill );
                        }

                        // Skip bot skill
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.BotSkill ) > 0 )
                            br.ReadByte();

                        // Set the dmflags
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.DMFlags ) > 0 ) {
                            // Get dmflags
                            int dmflags = br.ReadInt32();

                            // Get dmflags2
                            int dmflags2 = br.ReadInt32();

                            // Get compatflags
                            int compatflags = br.ReadInt32();

                            // Set several settings depending on dmflags and dmflags2
                            ht.Add( "dmflags1_nohealth", ( (DMFlags1)dmflags & DMFlags1.NoHealth ) > 0 );
                            ht.Add( "dmflags1_noitems", ( (DMFlags1)dmflags & DMFlags1.NoItems ) > 0 );
                            ht.Add( "dmflags1_weaponstay", ( (DMFlags1)dmflags & DMFlags1.WeaponStay ) > 0 );
                            ht.Add( "dmflags1_oldfalldamage", ( (DMFlags1)dmflags & DMFlags1.OldFallDamage ) > 0 );
                            ht.Add( "dmflags1_hexenfallingdamage", ( (DMFlags1)dmflags & DMFlags1.HexenFallDamage ) > 0 );
                            ht.Add( "dmflags1_samelevel", ( (DMFlags1)dmflags & DMFlags1.SameLevel ) > 0 );
                            ht.Add( "dmflags1_spawnfarthest", ( (DMFlags1)dmflags & DMFlags1.SpawnFarthest ) > 0 );
                            ht.Add( "dmflags1_forcerespawn", ( (DMFlags1)dmflags & DMFlags1.ForceRespawn ) > 0 );
                            ht.Add( "dmflags1_noarmor", ( (DMFlags1)dmflags & DMFlags1.NoArmor ) > 0 );
                            ht.Add( "dmflags1_noexit", ( (DMFlags1)dmflags & DMFlags1.NoExit ) > 0 );
                            ht.Add( "dmflags1_infiniteammo", ( (DMFlags1)dmflags & DMFlags1.InfiniteAmmo ) > 0 );
                            ht.Add( "dmflags1_nomonsters", ( (DMFlags1)dmflags & DMFlags1.NoMonsters ) > 0 );
                            ht.Add( "dmflags1_monsterrespawn", ( (DMFlags1)dmflags & DMFlags1.MonsterRespawn ) > 0 );
                            ht.Add( "dmflags1_itemrespawn", ( (DMFlags1)dmflags & DMFlags1.ItemRespawn ) > 0 );
                            ht.Add( "dmflags1_fastmonsters", ( (DMFlags1)dmflags & DMFlags1.FastMonsters ) > 0 );
                            ht.Add( "dmflags1_nojump", ( (DMFlags1)dmflags & DMFlags1.NoJumping ) > 0 );
                            ht.Add( "dmflags1_nofreelook", ( (DMFlags1)dmflags & DMFlags1.NoFreeLook ) > 0 );
                            ht.Add( "dmflags1_respawnsuper", ( (DMFlags1)dmflags & DMFlags1.RespawnSuper ) > 0 );
                            ht.Add( "dmflags1_nofov", ( (DMFlags1)dmflags & DMFlags1.NoFOV ) > 0 );
                            ht.Add( "dmflags1_noweaponspawn", ( (DMFlags1)dmflags & DMFlags1.NoWeaponSpawn ) > 0 );
                            ht.Add( "dmflags1_nocrouch", ( (DMFlags1)dmflags & DMFlags1.NoCrouching ) > 0 );
                            ht.Add( "dmflags1_cooploseinventory", ( (DMFlags1)dmflags & DMFlags1.CoopLoseInventory ) > 0 );
                            ht.Add( "dmflags1_cooplosekeys", ( (DMFlags1)dmflags & DMFlags1.CoopLoseKeys ) > 0 );
                            ht.Add( "dmflags1_cooploseweapons", ( (DMFlags1)dmflags & DMFlags1.CoopLoseWeapons ) > 0 );
                            ht.Add( "dmflags1_cooplosearmor", ( (DMFlags1)dmflags & DMFlags1.CoopLoseArmor ) > 0 );
                            ht.Add( "dmflags1_cooplosepowerups", ( (DMFlags1)dmflags & DMFlags1.CoopLosePowerups ) > 0 );
                            ht.Add( "dmflags1_cooploseammo", ( (DMFlags1)dmflags & DMFlags1.CoopLoseAmmo ) > 0 );
                            ht.Add( "dmflags1_coophalveammo", ( (DMFlags1)dmflags & DMFlags1.CoopHalfAmmo ) > 0 );

                            ht.Add( "dmflags2_weapondrop", ( (DMFlags2)dmflags2 & DMFlags2.WeaponDrop ) > 0 );
                            ht.Add( "dmflags2_norunes", ( (DMFlags2)dmflags2 & DMFlags2.NoRunes ) > 0 );
                            ht.Add( "dmflags2_instantreturn", ( (DMFlags2)dmflags2 & DMFlags2.InstantReturn ) > 0 );
                            ht.Add( "dmflags2_noteamswitch", ( (DMFlags2)dmflags2 & DMFlags2.NoTeamSwitch ) > 0 );
                            ht.Add( "dmflags2_noteamselect", ( (DMFlags2)dmflags2 & DMFlags2.NoTeamSelect ) > 0 );
                            ht.Add( "dmflags2_doubleammo", ( (DMFlags2)dmflags2 & DMFlags2.DoubleAmmo ) > 0 );
                            ht.Add( "dmflags2_degeneration", ( (DMFlags2)dmflags2 & DMFlags2.Degeneration ) > 0 );
                            ht.Add( "dmflags2_bfgfreeaim", ( (DMFlags2)dmflags2 & DMFlags2.BfgFreeAim ) > 0 );
                            ht.Add( "dmflags2_barrelrespawn", ( (DMFlags2)dmflags2 & DMFlags2.BarrelRespawn ) > 0 );
                            ht.Add( "dmflags2_norespawninvul", ( (DMFlags2)dmflags2 & DMFlags2.NoRespawnInvulnerability ) > 0 );
                            ht.Add( "dmflags2_shotgunstart", ( (DMFlags2)dmflags2 & DMFlags2.ShotgunStart ) > 0 );
                            ht.Add( "dmflags2_samespawnspot", ( (DMFlags2)dmflags2 & DMFlags2.SameSpawnSpot ) > 0 );
                            ht.Add( "dmflags2_sameteam", ( (DMFlags2)dmflags2 & DMFlags2.SameTeam ) > 0 );
                            ht.Add( "dmflags2_forcegldefaults", ( (DMFlags2)dmflags2 & DMFlags2.ForceGlDefaults ) > 0 );
                            ht.Add( "dmflags2_awarddamageinsteadkills", ( (DMFlags2)dmflags2 & DMFlags2.AwardDamageInsteadOfKills ) > 0 );
                            ht.Add( "dmflags2_coopspactorspawn", ( (DMFlags2)dmflags2 & DMFlags2.CoopSpActorSpawn ) > 0 );

                            ht.Add( "compatflags_shorttex", ( (CompatFlags1)compatflags & CompatFlags1.ShortTextures ) > 0 );
                            ht.Add( "compatflags_stairs", ( (CompatFlags1)compatflags & CompatFlags1.Stairs ) > 0 );
                            ht.Add( "compatflags_limitpain", ( (CompatFlags1)compatflags & CompatFlags1.LimitPain ) > 0 );
                            ht.Add( "compatflags_silentpickup", ( (CompatFlags1)compatflags & CompatFlags1.SilentPickup ) > 0 );
                            ht.Add( "compatflags_nopassover", ( (CompatFlags1)compatflags & CompatFlags1.NoPassOver ) > 0 );
                            ht.Add( "compatflags_soundslots", ( (CompatFlags1)compatflags & CompatFlags1.SoundSlots ) > 0 );
                            ht.Add( "compatflags_wallrun", ( (CompatFlags1)compatflags & CompatFlags1.WallRun ) > 0 );
                            ht.Add( "compatflags_notossdrops", ( (CompatFlags1)compatflags & CompatFlags1.NoTossDrops ) > 0 );
                            ht.Add( "compatflags_useblocking", ( (CompatFlags1)compatflags & CompatFlags1.UseBlocking ) > 0 );
                            ht.Add( "compatflags_nodoorlight", ( (CompatFlags1)compatflags & CompatFlags1.NoDoorLight ) > 0 );
                            ht.Add( "compatflags_ravenscroll", ( (CompatFlags1)compatflags & CompatFlags1.RavenScroll ) > 0 );
                            ht.Add( "compatflags_sectorsound", ( (CompatFlags1)compatflags & CompatFlags1.SectorSound ) > 0 );
                            ht.Add( "compatflags_dehhealth", ( (CompatFlags1)compatflags & CompatFlags1.DehHealth ) > 0 );
                            ht.Add( "compatflags_trace", ( (CompatFlags1)compatflags & CompatFlags1.Trace ) > 0 );
                            ht.Add( "compatflags_dropoff", ( (CompatFlags1)compatflags & CompatFlags1.Dropoff ) > 0 );
                            ht.Add( "compatflags_boomscroll", ( (CompatFlags1)compatflags & CompatFlags1.BoomScroll ) > 0 );
                            ht.Add( "compatflags_invisibility", ( (CompatFlags1)compatflags & CompatFlags1.Invisiblity ) > 0 );
                            ht.Add( "compatflags_limitedairmovement", ( (CompatFlags1)compatflags & CompatFlags1.LimitedAirMovement ) > 0 );
                            ht.Add( "compatflags_plasmabump", ( (CompatFlags1)compatflags & CompatFlags1.PlasmaBumpBug ) > 0 );
                            ht.Add( "compatflags_instantrespawn", ( (CompatFlags1)compatflags & CompatFlags1.InstantRespawn ) > 0 );
                            ht.Add( "compatflags_disabletaunts", ( (CompatFlags1)compatflags & CompatFlags1.DisableTaunts ) > 0 );
                            ht.Add( "compatflags_originalsoundcurve", ( (CompatFlags1)compatflags & CompatFlags1.OriginalSoundCurve ) > 0 );
                            ht.Add( "compatflags_oldintermission", ( (CompatFlags1)compatflags & CompatFlags1.OldIntermission ) > 0 );
                            ht.Add( "compatflags_disablestealthmonsters", ( (CompatFlags1)compatflags & CompatFlags1.DisableStealthMonsters ) > 0 );
                            ht.Add( "compatflags_oldradiusdmg", ( (CompatFlags1)compatflags & CompatFlags1.OldRadiusDMG ) > 0 );
                            ht.Add( "compatflags_nocrosshair", ( (CompatFlags1)compatflags & CompatFlags1.NoCrossHair ) > 0 );
                            ht.Add( "compatflags_oldweaponswitch", ( (CompatFlags1)compatflags & CompatFlags1.OldWeaponSwitch ) > 0 );
                        }

                        // Get the limits
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.Limits ) > 0 ) {
                            // Get fraglimit
                            short fraglimit = br.ReadInt16();
                            ht.Add( "general_fraglimit", fraglimit );

                            // Get timelimit
                            short timelimit = br.ReadInt16();
                            ht.Add( "general_timelimit", timelimit );

                            // Check if time left is added
                            if ( timelimit > 0 ) {
                                // Skip time left
                                br.ReadInt16();
                            }

                            // Get duellimit
                            short duellimit = br.ReadInt16();
                            ht.Add( "general_duellimit", duellimit );

                            // Get pointlimit
                            short pointlimit = br.ReadInt16();
                            ht.Add( "general_pointlimit", pointlimit );

                            // Get winlimit
                            short winlimit = br.ReadInt16();
                            ht.Add( "general_winlimit", winlimit );
                        }

                        // Get teamdamage
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamDamage ) > 0 ) {
                            float teamdamage = br.ReadSingle();
                            ht.Add( "general_teamdamage", (int)Math.Floor( teamdamage * 100 ) );
                        }

                        // Skip teamscores
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamScores ) > 0 ) {
                            // Skip blue team and red team (2 bytes each)
                            br.ReadInt32();
                        }

                        // Get number of players
                        byte numplayers = 0;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.NumPlayers ) > 0 )
                            numplayers = br.ReadByte();

                        // Check if there are any players
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.PlayerData ) > 0 ) {
                            string players = string.Empty;
                            string spectators = string.Empty;
                            string bots = string.Empty;

                            // Go for all players
                            for ( byte i = 0; i < numplayers; i++ ) {
                                // Get player name
                                string player; Helper.ReadNullTerminatedStringFromBinaryReader( br, "Player Name", out player );

                                // Discard player frags
                                br.ReadInt16();

                                // Discard player ping
                                br.ReadInt16();

                                // Get player spectating
                                byte spectate = br.ReadByte();

                                // Get player bot
                                byte bot = br.ReadByte();

                                // Add player based on type
                                if ( bot != 0 ) {
                                    if ( !string.IsNullOrEmpty( bots ) ) bots += "\t";
                                    bots += player;
                                } else if ( spectate != 0 ) {
                                    if ( !string.IsNullOrEmpty( spectators ) ) spectators += "\t";
                                    spectators += player;
                                } else {
                                    if ( !string.IsNullOrEmpty( players ) ) players += "\t";
                                    players += player;
                                }

                                // Discard team
                                if ( type == 4 || type == 8 || type >= 10 ) // 4, 8, 10, 11, 12, 13, 14, 15 
                                    br.ReadByte();

                                // Discard time
                                br.ReadByte();
                            }

                            // Strip color codes
                            bots = Helper.StripColorCodes( bots );
                            players = Helper.StripColorCodes( players );
                            spectators = Helper.StripColorCodes( spectators );

                            ht.Add( "bots", bots );
                            ht.Add( "players", players );
                            ht.Add( "spectators", spectators );
                        }

                        // Get number of teams
                        byte numTeams = 0;
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamInfoNumber ) > 0 )
                            numTeams = br.ReadByte();

                        // Go for all teams
                        for ( byte i = 0; i < numTeams; i++ ) {
                            // Discard team name
                            if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamInfoName ) > 0 )
                                Helper.ReadNullTerminatedStringFromBinaryReader( br, "Team Name", out strValue );

                            // Discard team color
                            if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamInfoColor ) > 0 )
                                br.ReadInt32();

                            // Discard team score
                            if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TeamInfoScore ) > 0 )
                                br.ReadInt16();
                        }

                        // Get debug build information
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.TestingServer ) > 0 ) {
                            // Discard test server
                            br.ReadByte();

                            // Discard test server information
                            Helper.ReadNullTerminatedStringFromBinaryReader( br, "Test Server Info", out strValue );
                        }

                        // Discard md5 hash (32 chars)
                        if ( ( (ServerQueryFlags)iBits & ServerQueryFlags.DataMD5Sum ) > 0 )
                            Helper.ReadNullTerminatedStringFromBinaryReader( br, "MD5 Hash", out strValue );

                        // Set additional flags
                        ht.Add( "settings_broadcast", true );
                        ht.Add( "missingfiles", sbMissingFiles.ToString().TrimEnd( '|' ) );
                        ht.Add( "invalidfiles", string.Empty );

                        // Close
                        br.Close();
                    }
                }
            }

            return ht;
        }

        // This is called when the plugin is terminated
        public override void Dispose() {
            // Cleanup
            _me = null;
            _serverProc = null;

            base.Dispose();
        }
    }
}
