using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ReadyPlayerMe.Core.Editor
{
    /// <summary>
    ///     Class <c>ModuleInstaller</c> is responsible for checking and installing all modules (Unity packages) required for
    ///     the Ready Player Me Unity SDK from their Git URL's.
    /// </summary>
    [InitializeOnLoad]
    public static class ModuleInstaller
    {
        private const string TAG = nameof(ModuleInstaller);

        private const int THREAD_SLEEP_TIME = 100;
        private const string PROGRESS_BAR_TITLE = "Ready Player Me";

        private const string MODULE_INSTALLATION_SUCCESS_MESSAGE =
            "All the modules are installed successfully. Ready Player Me avatar system is ready to use.";
        private const string MODULE_INSTALLATION_FAILURE_MESSAGE = "Something went wrong while installing modules.";
        private const string ALL_MODULES_ARE_INSTALLED = "All modules are installed.";
        private const string INSTALLING_MODULES = "Installing modules...";

        private const float TIMEOUT_FOR_MODULE_INSTALLATION = 20f;

        static ModuleInstaller()
        {
            Events.registeringPackages -= OnRegisteringPackages;
            Events.registeringPackages += OnRegisteringPackages;

#if !READY_PLAYER_ME
            InstallModules();
            EditorApplication.delayCall += DelayCreateCoreSettings;
            DefineSymbolHelper.AddSymbols();
#endif
        }

        /// <summary>
        ///     Called when a package is about to be added, removed or changed.
        /// </summary>
        /// <param name="args">Describes the <c>PackageInfo</c> entries of packages currently registering.</param>
        private static void OnRegisteringPackages(PackageRegistrationEventArgs args)
        {
            // Core module uninstalled
            if (args.removed != null && args.removed.Any(p => p.name == ModuleList.Core.name))
            {
                DefineSymbolHelper.RemoveSymbols();
                ProjectPrefs.SetBool(ProjectPrefs.FIRST_TIME_SETUP_DONE, false);
            }
        }

        private static void DelayCreateCoreSettings()
        {
            EditorApplication.delayCall -= DelayCreateCoreSettings;
            CoreSettingsLoader.EnsureSettingsExist();

        }

        /// <summary>
        ///     Installs the missing modules and displays a progress bar to notify the user.
        /// </summary>
        private static void InstallModules()
        {
            EditorUtility.DisplayProgressBar(PROGRESS_BAR_TITLE, INSTALLING_MODULES, 0);
            Thread.Sleep(THREAD_SLEEP_TIME);

            var missingModules = GetMissingModuleNames();
            if (missingModules.Length > 0)
            {
                var installedModuleCount = 0f;

                foreach (ModuleInfo module in missingModules)
                {
                    var progress = installedModuleCount++ / missingModules.Length;
                    EditorUtility.DisplayProgressBar(PROGRESS_BAR_TITLE, $"Installing module {module.name}", progress);
                    AddModuleRequest(module.Identifier);
                }

                EditorUtility.DisplayProgressBar(PROGRESS_BAR_TITLE, ALL_MODULES_ARE_INSTALLED, 1);
                Thread.Sleep(THREAD_SLEEP_TIME);
            }
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        ///     Request UPM to install the given module with the identifier.
        /// </summary>
        /// <param name="identifier">The Unity package identifier of the module to be installed.</param>
        public static void AddModuleRequest(string identifier)
        {
            var startTime = Time.realtimeSinceStartup;
            AddRequest addRequest = Client.Add(identifier);
            while (!addRequest.IsCompleted && Time.realtimeSinceStartup - startTime < TIMEOUT_FOR_MODULE_INSTALLATION)
                Thread.Sleep(THREAD_SLEEP_TIME);

            if (Time.realtimeSinceStartup - startTime >= TIMEOUT_FOR_MODULE_INSTALLATION)
            {
                Debug.LogError($"Package installation timed out for {identifier}. Please try again.");
            }
            if (addRequest.Error != null)
            {
                Debug.LogError("Error: " + addRequest.Error.message);
            }
        }

        /// <summary>
        ///     Get modules from <c>ModuleList</c> that are not installed.
        /// </summary>
        /// <returns>An array of <c>ModuleInfo</c> for all the missing modules</returns>
        private static ModuleInfo[] GetMissingModuleNames()
        {
            PackageInfo[] installed = GetPackageList();
            var missingModules = ModuleList.Modules.Where(m => installed.All(i => m.name != i.name)).ToList();
            return missingModules.ToArray();
        }

        /// <summary>
        ///     Check if the given module with the name is currently installed.
        /// </summary>
        /// <param name="name">Name of the module.</param>
        /// <returns>A boolean <c>true</c> if the module is installed.</returns>
        public static bool IsModuleInstalled(string name)
        {
            return GetPackageList().Any(info => info.name == name);
        }

        /// <summary>
        ///     Get the list of unity packages installed in the current project.
        /// </summary>
        /// <returns>An array of <c>PackageInfo</c>.</returns>
        public static PackageInfo[] GetPackageList()
        {
            ListRequest listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
                Thread.Sleep(THREAD_SLEEP_TIME);

            if (listRequest.Error != null)
            {
                SDKLogger.Log(TAG, "Error: " + listRequest.Error.message);
                return Array.Empty<PackageInfo>();
            }

            return listRequest.Result.ToArray();
        }

        /// <summary>
        ///     Check all modules installed successfully
        /// </summary>
        private static void ValidateModules()
        {
            PackageInfo[] packageList = GetPackageList();
            var allModuleInstalled = true;
            foreach (ModuleInfo module in ModuleList.Modules)
            {
                if (packageList.All(x => x.name != module.name))
                {
                    allModuleInstalled = false;
                }
            }

            if (allModuleInstalled)
            {
                SDKLogger.Log(TAG, MODULE_INSTALLATION_SUCCESS_MESSAGE);
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
            }
            else
            {
                SDKLogger.LogWarning(TAG, MODULE_INSTALLATION_FAILURE_MESSAGE);
            }
        }
    }
}
