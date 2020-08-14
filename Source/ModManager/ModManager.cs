﻿//#define DEBUG_PROFILE

using System.Reflection;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using Verse;

namespace ModManager
{
    public class ModManager: Mod
    {
        public ModManager( ModContentPack content ) : base( content )
        {
            Instance = this;
            UserData = new UserData();
            Settings = GetSettings<ModManagerSettings>();

            var harmonyInstance = new Harmony( "fluffy.modmanager" );

#if DEBUG
            Harmony.DEBUG = true;
#endif
            harmonyInstance.PatchAll( Assembly.GetExecutingAssembly() );

#if DEBUG_PROFILE
            LongEventHandler.ExecuteWhenFinished( () => new Profiler( typeof( Page_BetterModConfig ).GetMethod(
                                                                          nameof( Page_BetterModConfig.DoWindowContents
                                                                          ) ) ) );
#endif
        }

        public static ModManager Instance { get; private set; }

        public static UserData           UserData { get; private set; }
        public static ModManagerSettings Settings { get; private set; }

        public override string SettingsCategory() => I18n.SettingsCategory;

        public override void DoSettingsWindowContents( Rect canvas )
        {
            base.DoSettingsWindowContents( canvas );
            var listing = new Listing_Standard();
            listing.ColumnWidth = canvas.width;
            listing.Begin( canvas );
            listing.CheckboxLabeled( I18n.ShowAllRequirements, ref Settings.ShowSatisfiedRequirements,
                                     I18n.ShowAllRequirementsTip );
            listing.CheckboxLabeled( I18n.ShowVersionChecksForSteamMods, ref Settings.ShowVersionChecksOnSteamMods,
                                     I18n.ShowVersionChecksForSteamModsTip );

            listing.Gap();
            listing.CheckboxLabeled( I18n.ShowPromotions, ref Settings.ShowPromotions, I18n.ShowPromotionsTip );

            if ( !Settings.ShowPromotions )
                GUI.color = Color.grey;

            listing.CheckboxLabeled( I18n.ShowPromotions_NotSubscribed, ref Settings.ShowPromotions_NotSubscribed );
            listing.CheckboxLabeled( I18n.ShowPromotions_NotActive, ref Settings.ShowPromotions_NotActive );

            GUI.color = Color.white;
            listing.Gap();

            listing.CheckboxLabeled( I18n.TrimTags, ref Settings.TrimTags, I18n.TrimTagsTip );
            if ( !Settings.TrimTags )
                GUI.color = Color.grey;
            listing.CheckboxLabeled( I18n.TrimVersionStrings, ref Settings.TrimVersionStrings,
                                     I18n.TrimVersionStringsTip );

            GUI.color = Color.white;
            listing.Gap();
            listing.CheckboxLabeled( I18n.AddModManagerToNewModList, ref Settings.AddModManagerToNewModLists,
                                     I18n.AddModManagerToNewModListTip );
            listing.CheckboxLabeled( I18n.AddHugsLibToNewModList, ref Settings.AddHugsLibToNewModLists,
                                     I18n.AddHugsLibToNewModListTip);
            listing.CheckboxLabeled( I18n.AddExpansionsToNewModList, ref Settings.AddExpansionsToNewModLists,
                                     I18n.AddExpansionsToNewModListTip );
            listing.End();
        }
    }
}