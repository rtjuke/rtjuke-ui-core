using CommunityToolkit.Mvvm.Input;
using RTJuke.Core.Plugins;
using RTJuke.Core.Types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;

namespace RTJuke.UICore.ViewModels
{
    public class PluginViewModel : ViewModelBase
    {
        #region Properties
        public LoadedPlugin Plugin { get; private set; }

        /// <summary>
        /// If the plugin is disabled its songs will be hidden
        /// from the library
        /// </summary>
        public bool Enabled
        {
            get
            {
                return true; // Todo
            }
            set
            {
                OnPropertyChanged();
            }
        }

        public string Name => Plugin.PluginInfo.Name;

        public string Description => Plugin.PluginInfo.Description;

        public string ProviderId => Plugin.ProviderId;

        public string HumanFriendlySettingsText => Plugin.Instance.HumanFriendlySettingsText;

        public string Version => Plugin.PluginInfo.Version.ToString();

        public IPlugin Instance => Plugin.Instance;

        #endregion

        #region Commands

        public ICommand ConfigureCommand { get; private set; }

        #endregion

        #region Constructor
        public PluginViewModel(LoadedPlugin plugin)
        {
            Plugin = plugin;

            ConfigureCommand = new RelayCommand(ConfigureCommand_Execute, ConfigureCommand_CanExecute);
        }
        #endregion

        #region Command implementations
        protected void ConfigureCommand_Execute()
        {
            if (Plugin.Instance.Configure())
            {
                OnPropertyChanged(nameof(HumanFriendlySettingsText));
            }
        }

        protected bool ConfigureCommand_CanExecute()
        {
            return Plugin.Instance.CanBeConfigured();
        }

        #endregion
    }
}
