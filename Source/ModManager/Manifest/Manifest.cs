﻿// Manifest.cs
// Copyright Karel Kroeze, 2018-2018

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;
using static ModManager.Constants;

namespace ModManager
{
    public class Manifest
    {
        private string version;
        private ModMetaData mod;
        public Version Version { get; private set; }
        public string identifier;
        public string manifestUri;
        public string downloadUri;
        public string changenote;
        public List<Dependency> dependencies = new List<Dependency>();
        public List<Dependency> incompatibleWith = new List<Dependency>();
        public List<string> loadBefore = new List<string>();
        public List<string> loadAfter = new List<string>();
        private OnlineManifest _onlineManifest;

        public Texture2D Icon
        {
            get
            {
                // own version unknown, not implemented.
                if ( Version == null )
                    return Resources.Status_Cross;

                // no manifest uri, not implemented.
                if ( manifestUri.NullOrEmpty() )
                    return Resources.Status_Cross;

                // manifest uri set, onlineManifest status.
                if ( OnlineManifest.Status != OnlineManifest.WWWStatus.Done )
                    return OnlineManifest.Icon;

                // onlineManifest done, version status.
                if ( OnlineManifest.manifest.Version == null )
                    return Resources.Warning;

                if ( OnlineManifest.manifest.Version >= Version )
                    return Resources.Status_Up;

                return Widgets.CheckboxOnTex;
            }
        }

        public OnlineManifest OnlineManifest
        {
            get
            {
                if ( _onlineManifest == null )
                    _onlineManifest = new OnlineManifest( manifestUri );
                return _onlineManifest;
            }
        }
        
        public ModButton_Installed Button => ModButton_Installed.For( mod );

        private const string ManifestFileName = "Manifest.xml";
        private const string ModSyncFileName = "ModSync.xml";
        private const string AssembliesFolder = "Assemblies";
        
        private static Dictionary<ModMetaData, Manifest> _manifestCache = new Dictionary<ModMetaData, Manifest>();

        public Manifest() { }

        public Manifest( ModMetaData mod )
        {
            this.mod = mod;
        }

        public Manifest( ModMetaData mod, string version, string identifier )
        {
            this.mod = mod;
            this.version = version;
            this.identifier = identifier;
        }

        private List<ModIssue> _issues;

        public void Notify_RecacheIssues()
        {
            _issues = null;
        }
        public List<ModIssue> Issues
        {
            get
            {
                if ( _issues == null )
                    _issues = DependencyIsues
                        .Concat( LoadOrderIssues )
                        .Concat( IncompatibilityIssues )
                        .ToList();
                return _issues;
            }
        }

        public List<ModIssue> DependencyIsues
        {
            get
            {
                var issues = new List<ModIssue>();
                foreach ( var dependency in dependencies )
                {
                    switch ( dependency.Met )
                    {
                        case DependencyStatus.UnknownVersion:
                            issues.Add( new ModIssue( Severity.Minor, Subject.Dependency,
                                ModButton_Installed.For( mod ), dependency.Identifier,
                                I18n.DependencyUnknownVersion( dependency, dependency.Target ) ) );
                            break;
                        case DependencyStatus.WrongVersion:
                            issues.Add( new ModIssue( Severity.Major, Subject.Dependency,
                                ModButton_Installed.For( mod ), dependency.Identifier,
                                I18n.DependencyWrongVersion( dependency, dependency.Target ) ) );
                            break;
                        case DependencyStatus.NotFound:
                            issues.Add( new ModIssue( Severity.Critical, Subject.Dependency,
                                ModButton_Installed.For( mod ), dependency.Identifier,
                                I18n.DependencyNotFound( dependency.Identifier ) ) );
                            break;
                    }
                }
                return issues;
            }
        }

        public List<ModIssue> IncompatibilityIssues
        {
            get
            {
                var issues = new List<ModIssue>();
                foreach ( var dependency in incompatibleWith )
                {
                    if ( dependency.Met == DependencyStatus.Met || dependency.Met == DependencyStatus.UnknownVersion )
                        issues.Add( new ModIssue( Severity.Major, Subject.Other, Button, dependency.Identifier,
                            I18n.IncompatibleMod( mod.Name, dependency.Identifier ),
                            () => Resolvers.ResolveIncompatible( Button, dependency.Target ) ) );
                }
                return issues;
            }
        }

        public List<ModIssue> LoadOrderIssues
        {
            get
            {
                var issues = new List<ModIssue>();
                var order = mod.LoadOrder();
                foreach ( var id in loadAfter
                    .Concat( dependencies
                    .Where( d => d.Met == DependencyStatus.Met || d.Met == DependencyStatus.UnknownVersion )
                    .Select( d => d.Identifier ) ) )
                {
                    var otherMod = ModButtonManager.ActiveButtons
                        .OfType<ModButton_Installed>()
                        .FirstOrDefault( m => m.MatchesIdentifier( id ) );
                    var otherOrder = otherMod?.LoadOrder;
                    if ( otherOrder > order )
                        issues.Add( new ModIssue( Severity.Major, Subject.LoadOrder,
                            Button, otherMod.Identifier,
                            I18n.ShouldBeLoadedAfter( otherMod.Name ),
                            () => Resolvers.ResolveShouldLoadAfter( Button, otherMod ) ) );
                }
                foreach ( var id in loadBefore )
                {
                    var otherMod = ModButtonManager.ActiveButtons
                        .OfType<ModButton_Installed>()
                        .FirstOrDefault(m => m.MatchesIdentifier(id));
                    var otherOrder = otherMod?.LoadOrder;
                    if ( otherOrder >= 0 && otherOrder < order )
                        issues.Add( new ModIssue( Severity.Major, Subject.LoadOrder,
                            Button, otherMod.Identifier,
                            I18n.ShouldBeLoadedBefore( otherMod.Name ),
                            () => Resolvers.ResolveShouldLoadBefore( Button, otherMod ) ) );
                }
                return issues;
            }
        }

        public override string ToString()
        {
            var str = $"Manifest for: {mod.Name} ({Version?.ToString() ?? "unknown" })";
            if ( manifestUri != null ) str += $"\n\tmanifestUri: {manifestUri}";
            if (downloadUri != null ) str += $"\n\tdownloadUri: {downloadUri}";
            if (changenote != null ) str += $"\n\tchangenote: {changenote}";
            if (!dependencies.NullOrEmpty())
                dependencies.ForEach(d => str += $"\n\tdependency: {d}");
            if (!incompatibleWith.NullOrEmpty())
                incompatibleWith.ForEach(d => str += $"\n\tincompatibleWith: {d}");
            if (!loadBefore.NullOrEmpty())
                loadBefore.ForEach(d => str += $"\n\tloadBefore: {d}");
            if (!loadAfter.NullOrEmpty())
                loadAfter.ForEach(d => str += $"\n\tloadAfter: {d}");
            return str;
        }

        public static Manifest For( ModMetaData mod )
        {
            Manifest manifest;
            if ( _manifestCache.TryGetValue( mod, out manifest ) )
                return manifest;

            manifest = new Manifest();
            manifest.mod = mod;

            // get from file.
            var manifestPath = Path.Combine( mod.AboutDir(), ManifestFileName );
            var modsyncPath = Path.Combine( mod.AboutDir(), ModSyncFileName );

            // manifest is first choice
            if ( File.Exists( manifestPath ) )
            {
                try
                {
                    manifest = DirectXmlLoader.ItemFromXmlFile<Manifest>( manifestPath );
                    manifest.mod = mod;
                    manifest.dependencies.ForEach( d => d.Owner = manifest );
                }
                catch ( Exception e )
                {
                    manifest = new Manifest( mod );
                    Log.Error( $"Error loading manifest for '{mod.Name}':\n{e.Message}\n\n{e.StackTrace}" );
                }
            }

            // modsync manifest can provide some info
            else if ( File.Exists( modsyncPath ) )
            {
                try
                {
                    ModSync modsync = DirectXmlLoader.ItemFromXmlFile<ModSync>( modsyncPath );
                    manifest = modsync.Manifest( mod );
                }
                catch ( Exception e )
                {
                    manifest = new Manifest( mod );
                    Log.Error( $"Error loading ModSync into manifest for '{mod.Name}': {e.Message}\n\n{e.StackTrace}" );
                }
            }

            // resolve version - if set in manifest or modsync that takes priority,
            // otherwise try to read version from assemblies.
            manifest.SetVersion();
            _manifestCache.Add( mod, manifest );
            return manifest;
        }

        private Version ParseVersion( string version )
        {
            try
            {
                return new Version( version );
            }
            catch
            {
                try
                {
                    var pattern = @"[^0-9\.]";
                    return new Version( Regex.Replace( version, pattern, "" ) );
                }
                catch ( Exception e )
                {
                    Log.Warning( $"Failed to parse version string '{version}': {e.Message}\n\n{e.StackTrace}" );
                    return null;
                }
            }
        }

        public void SetVersion( bool fromAssemblies = true )
        {
            if ( !version.NullOrEmpty() )
            {
                // if version was set, this is simple
                Version = ParseVersion( version );
            }
            else if ( fromAssemblies )
            {
                // if the mod is loaded, we may be able to get a version from its assembly/ies.
                var loaded = LoadedModManager.RunningModsListForReading.FirstOrDefault(mcp => mcp.Identifier == mod.Identifier);

                // under most circumstances, any depencies (e.g. Harmony) are loaded first - the last assembly is likely to be the main assembly
                if ( loaded != null )
                {
                    // any assemblies will already be loaded
                    if ( loaded.LoadedAnyAssembly )
                    {
                        Version = loaded.assemblies.loadedAssemblies.LastOrDefault()?.GetName().Version;
                    }
                }
                else
                {
                    // not loaded, try get version from files
                    var assemblyFolder = new DirectoryInfo( Path.Combine( mod.RootDir.FullName, AssembliesFolder ) );
                    if ( assemblyFolder.Exists )
                    {
                        var assemblies = assemblyFolder.GetFiles( "*.dll" );
                        if ( !assemblies.NullOrEmpty() )
                        {
                            Version = ParseVersion( FileVersionInfo.GetVersionInfo( assemblies.Last().FullName )
                                .FileVersion );
                        }
                    }
                }
            }   
        }

        public void DoDependencyDetails(ref Rect canvas )
        {
            if (dependencies.NullOrEmpty())
                return;

            Utilities.DoLabel( ref canvas, I18n.Dependencies );
            var outRect = new Rect( canvas ){height = Mathf.CeilToInt( dependencies.Count / 3f ) * LineHeight + SmallMargin * 2f};
            Widgets.DrawBoxSolid(outRect, Resources.SlightlyDarkBackground);
            canvas.yMin += outRect.height + SmallMargin;
            outRect = outRect.ContractedBy(SmallMargin);
            var dependencyRect = new Rect(
                outRect.xMin,
                outRect.yMin,
                outRect.width / 3f,
                LineHeight );

            int i = 1;
            foreach (var dependency in dependencies)
            {
                dependency.Draw(dependencyRect);
                if (i++ % 3 == 0)
                {
                    dependencyRect.x = outRect.xMin;
                    dependencyRect.y += LineHeight;
                }
                else
                {
                    dependencyRect.x += dependencyRect.width;
                }
            }
        }
    }
}