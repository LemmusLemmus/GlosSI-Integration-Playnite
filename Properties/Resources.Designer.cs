﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GlosSIIntegration.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("GlosSIIntegration.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap DefaultSteamShortcutIcon {
            get {
                object obj = ResourceManager.GetObject("DefaultSteamShortcutIcon", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {
        ///    &quot;controller&quot;: {
        ///        &quot;allowDesktopConfig&quot;: false,
        ///        &quot;emulateDS4&quot;: false,
        ///        &quot;maxControllers&quot;: 4
        ///    },
        ///    &quot;devices&quot;: {
        ///        &quot;hideDevices&quot;: true,
        ///        &quot;realDeviceIds&quot;: false
        ///    },
        ///    &quot;icon&quot;: &quot;&lt;icon&gt;&quot;,
        ///    &quot;launch&quot;: {
        ///        &quot;closeOnExit&quot;: false,
        ///        &quot;launch&quot;: false,
        ///        &quot;launchAppArgs&quot;: null,
        ///        &quot;launchPath&quot;: null,
        ///        &quot;waitForChildProcs&quot;: false
        ///    },
        ///    &quot;name&quot;: &quot;&lt;name&gt;&quot;,
        ///    &quot;version&quot;: 1,
        ///    &quot;window&quot;: {
        ///        &quot;disableOverlay&quot;: false,
        /// [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string DefaultTarget {
            get {
                return ResourceManager.GetString("DefaultTarget", resourceCulture);
            }
        }
    }
}
