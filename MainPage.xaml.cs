using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using NovaMac.Models;
using NovaMac.Services;

namespace NovaMac;

public sealed partial class MainPage : Page
{
    private readonly AppSettingsService _settingsService = App.Settings;
    private readonly VmStorageService _storageService;
    private readonly AppleRecoveryService _recoveryService = new();
    private readonly OpenCoreService _openCoreService = new();
    private readonly QemuService _qemuService;
    private readonly List<MacOSRelease> _catalog = MacOSRelease.GetCatalog();

    private List<VirtualMachineConfig> _allVms = [];
    private VirtualMachineConfig _draftVm = new();
    private VirtualMachineConfig? _editingVm;
    private string? _customLocalImagePath;
    private CancellationTokenSource? _creationCts;
    private bool _isLoadingSettingsUi;
    private bool _isPopulatingEditorUi;

    public UIElement TitleBarElement => AppTitleBar;

    public MainPage()
    {
        _storageService = new VmStorageService(_settingsService);
        _qemuService = new QemuService(_settingsService);

        InitializeComponent();

        CatalogGridView.ItemsSource = _catalog;
        LoadSettingsIntoUi();
        LoadVmsIntoUi();
        OpenVmEditorForCreate(switchToView: false);
        _ = RefreshDiagnosticsUiAsync();
    }

    private void ShowNotification(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private void LoadVmsIntoUi()
    {
        _allVms = _storageService.LoadAllVms();
        foreach (var vm in _allVms)
        {
            vm.IsRunning = _qemuService.IsVmRunning(vm.Id);
            vm.ProcessId = _qemuService.GetVmProcessId(vm.Id);
        }
        ApplyVmFilters();
    }

    private void ApplyVmFilters()
    {
        if (VmsItemsControl == null)
            return;

        string query = TxtSearchVms?.Text?.Trim() ?? string.Empty;
        string versionFilter = CmbFilterVersion?.SelectedItem?.ToString() ?? "Todas as Versões do macOS";

        var filtered = _allVms.Where(vm =>
        {
            bool matchesVersion = versionFilter == "Todas as Versões do macOS" ||
                                  vm.MacOSName.Contains(versionFilter, StringComparison.OrdinalIgnoreCase);

            bool matchesQuery = string.IsNullOrEmpty(query) ||
                                vm.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                vm.MacOSName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                vm.MacOSVersion.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                vm.SerialNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                vm.SshPortForward.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);

            return matchesVersion && matchesQuery;
        }).ToList();

        VmsItemsControl.ItemsSource = null;
        VmsItemsControl.ItemsSource = filtered;

        if (TxtVmCountSummary != null)
        {
            int runningCount = _allVms.Count(v => v.IsRunning);
            TxtVmCountSummary.Text = runningCount > 0
                ? $"{filtered.Count} de {_allVms.Count} VMs ({runningCount} ativa)"
                : $"{filtered.Count} de {_allVms.Count} VMs";
        }

        if (EmptyLibraryCard != null)
        {
            EmptyLibraryCard.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void TxtSearchVms_TextChanged(object sender, TextChangedEventArgs e) => ApplyVmFilters();

    private void CmbFilterVersion_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyVmFilters();

    private async Task RefreshDiagnosticsUiAsync()
    {
        var diag = await _qemuService.RunDiagnosticsAsync();

        string qemuLabel = diag.QemuInstalled
            ? (diag.QemuExecutablePath.Contains("EmbeddedQemu", StringComparison.OrdinalIgnoreCase) ? "QEMU Embutido OK" : "QEMU OK")
            : "Sem QEMU";

        TxtPaneSubtitle.Text = $"{qemuLabel} • {(diag.WhpxAvailable ? "WHPX disponível" : "WHPX indisponível")}";
        TxtDiagQemu.Text = diag.QemuInstalled ? $"Instalado ({diag.QemuVersion})" : "Não detectado no PATH ou Program Files";
        TxtDiagQemuPath.Text = diag.QemuExecutablePath;
        TxtDiagWhpx.Text = diag.WhpxAvailable
            ? "Hipervisor do Windows disponível"
            : "Não detectado (Ative o HypervisorPlatform no Windows)";
        TxtDiagCpu.Text = $"{diag.CpuBrand} ({diag.LogicalProcessors} processadores lógicos)";
    }

    private void SwitchView(string tag)
    {
        ViewLibrary.Visibility = tag == "Library" ? Visibility.Visible : Visibility.Collapsed;
        ViewWizard.Visibility = tag == "Create" ? Visibility.Visible : Visibility.Collapsed;
        ViewDiagnostics.Visibility = tag == "Diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        ViewSettings.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void MainNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            LoadSettingsIntoUi();
            SwitchView("Settings");
            return;
        }

        if (args.SelectedItemContainer?.Tag is string tag)
        {
            if (tag == "Library")
                LoadVmsIntoUi();
            else if (tag == "Diagnostics")
                await RefreshDiagnosticsUiAsync();
            else if (tag == "Settings")
                LoadSettingsIntoUi();

            SwitchView(tag);
        }
    }

    private void BtnGoToCreateNewVm_Click(object sender, RoutedEventArgs e)
    {
        OpenVmEditorForCreate(switchToView: true);
    }

    private void BtnBackToLibrary_Click(object sender, RoutedEventArgs e)
    {
        MainNav.SelectedItem = NavItemLibrary;
        LoadVmsIntoUi();
        SwitchView("Library");
    }

    private void BtnRefreshVms_Click(object sender, RoutedEventArgs e)
    {
        LoadVmsIntoUi();
    }

    private async void ToggleLightDarkThemeAsync()
    {
        var current = _settingsService.Current;
        var currentActual = this.ActualTheme;
        string nextTheme = currentActual == ElementTheme.Dark ? "Light" : "Dark";

        current.Theme = nextTheme;
        App.ApplyTheme(nextTheme);
        await _settingsService.SaveAsync(current);
        LoadSettingsIntoUi();

        string label = nextTheme == "Light" ? "Modo Claro (Light)" : "Modo Escuro (Dark)";
        ShowNotification("Tema Alterado", $"O aplicativo agora está em {label}.", InfoBarSeverity.Informational);
    }

    private void BtnQuickTheme_Click(object sender, RoutedEventArgs e) => ToggleLightDarkThemeAsync();

    private void NavToggleTheme_Tapped(object sender, TappedRoutedEventArgs e) => ToggleLightDarkThemeAsync();

    // ==================== DEDICATED VM CREATE & EDIT PAGE LOGIC ====================

    private void OpenVmEditorForCreate(bool switchToView)
    {
        _editingVm = null;
        _isPopulatingEditorUi = true;
        try
        {
            TxtVmEditorHeaderTitle.Text = "Criar Nova Máquina Virtual macOS";
            TxtVmEditorModeBadge.Text = "MODO CRIAÇÃO";
            TxtVmEditorSubtitle.Text = "Selecione a versão desejada do macOS (do Big Sur 11 ao Sequoia 15, Tahoe 26 e macOS 27) e personalize todos os parâmetros da VM.";

            BtnSwitchToCreateMode.Visibility = Visibility.Collapsed;
            BtnSaveEditedVm.Visibility = Visibility.Collapsed;
            BtnCreateVm.Visibility = Visibility.Visible;

            TxtProgressStage.Text = "Pronto para baixar a imagem oficial de recuperação e criar o pacote de VM (.novavm).";
            TxtProgressDetails.Text = "O OpenCore + OVMF UEFI e o BaseSystem.dmg da Apple serão armazenados em cache para reutilização instantânea.";
            CreationProgressBar.Value = 0;

            var (sshPort, vncPort) = _storageService.GetNextAvailablePorts();
            TxtVmSshPort.Text = sshPort.ToString();
            TxtVmVncPort.Text = vncPort.ToString();

            ApplyNewVmHardwareDefaults();
            TglMountRecovery.IsOn = true;
            CmbVgaAdapter.SelectedIndex = 0;
            CmbNetworkCard.SelectedIndex = 0;
            TxtSharedFolder.Text = string.Empty;
            TxtCustomQemuArgs.Text = string.Empty;
            TxtOpenCoreBootArgs.Text = "-v keepsyms=1 debug=0x100 agdpmod=pikera revpatch=sbvmm,cpuname -lilubetaall -cryptbeta";
        }
        finally
        {
            _isPopulatingEditorUi = false;
        }

        CatalogGridView.SelectedIndex = 2; // Default macOS Sequoia 15
        ApplyReleaseDefaultsToEditor(_catalog[2]);

        if (switchToView)
        {
            MainNav.SelectedItem = NavItemVmEditor;
            SwitchView("Create");
        }
    }

    private void OpenVmEditorForEdit(VirtualMachineConfig vm)
    {
        _editingVm = vm;
        _isPopulatingEditorUi = true;
        try
        {
            TxtVmEditorHeaderTitle.Text = $"Configurações da VM: {vm.Name}";
            TxtVmEditorModeBadge.Text = $"EDITANDO • {vm.MacOSName.ToUpperInvariant()}";
            TxtVmEditorSubtitle.Text = $"Pacote de VM localizado em: {vm.BundleDirectory}";

            BtnSwitchToCreateMode.Visibility = Visibility.Visible;
            BtnSaveEditedVm.Visibility = Visibility.Visible;
            BtnCreateVm.Visibility = Visibility.Collapsed;

            TxtProgressStage.Text = $"Editando máquina virtual '{vm.Name}' (ID: {vm.Id}).";
            TxtProgressDetails.Text = "Clique em 'Salvar Alterações da VM' para gravar o config.json e atualizar o OpenCore-config.plist.";
            CreationProgressBar.Value = 100;

            int catalogIdx = _catalog.FindIndex(c => c.Id == vm.ReleaseId);
            int selectedIdx = catalogIdx >= 0 ? catalogIdx : 2;
            CatalogGridView.SelectedIndex = selectedIdx;
            var currentRelease = _catalog[selectedIdx];
            if (CmbExactVersion != null)
            {
                CmbExactVersion.ItemsSource = currentRelease.SubVersions;
                int subIdx = currentRelease.SubVersions.FindIndex(s => s.Version.Equals(vm.MacOSVersion, StringComparison.OrdinalIgnoreCase));
                CmbExactVersion.SelectedIndex = subIdx >= 0 ? subIdx : 0;
            }

            TxtVmName.Text = vm.Name;

            SelectEditorOption(CmbCpuCores, vm.CpuCores.ToString());
            SelectEditorOption(CmbRamGb, vm.RamGb.ToString());
            SelectEditorOption(CmbDiskGb, vm.DiskSizeGb.ToString());
            SelectEditorOption(CmbResolution, vm.Resolution);

            CmbVgaAdapter.SelectedIndex = vm.VgaAdapter switch
            {
                "vmware" => 1,
                "virtio" => 2,
                "cirrus" => 3,
                _ => 0
            };

            CmbCpuProfile.SelectedIndex = vm.CpuProfile switch
            {
                "Cascadelake-Server" => 0,
                "Haswell-noTSX" => 1,
                _ => 2
            };

            CmbNetworkCard.SelectedIndex = vm.NetworkCard == "e1000-82545em" ? 1 : 0;
            CmbSmbiosModel.SelectedIndex = vm.SmbiosModel == "iMacPro1,1" ? 1 : 0;

            TglMountRecovery.IsOn = vm.MountRecoveryDrive;
            TglEnableAudio.IsOn = vm.EnableAudio;
            TglFullscreen.IsOn = vm.Fullscreen;

            TxtVmSshPort.Text = vm.SshPortForward.ToString();
            TxtVmVncPort.Text = vm.VncPortForward.ToString();
            TxtSharedFolder.Text = vm.SharedFolderPath;

            TxtSerialNumber.Text = vm.SerialNumber;
            TxtBoardSerial.Text = vm.BoardSerialNumber;
            TxtSystemUuid.Text = vm.SystemUuid;
            TxtRomMac.Text = vm.RomMacAddress;
            TxtCustomQemuArgs.Text = vm.CustomQemuArgs;
            TxtOpenCoreBootArgs.Text = vm.OpenCoreBootArgs;
        }
        finally
        {
            _isPopulatingEditorUi = false;
        }

        MainNav.SelectedItem = NavItemVmEditor;
        SwitchView("Create");
    }

    private void CatalogGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPopulatingEditorUi || CatalogGridView.SelectedItem is not MacOSRelease release)
            return;

        if (_editingVm == null)
        {
            ApplyReleaseDefaultsToEditor(release);
        }
    }

    private void ApplyReleaseDefaultsToEditor(MacOSRelease release)
    {
        _customLocalImagePath = null;
        if (TxtCustomImageSelected != null)
            TxtCustomImageSelected.Visibility = Visibility.Collapsed;

        // Configure SubVersions and Beta/Stable Selector
        if (CmbExactVersion != null)
        {
            _isPopulatingEditorUi = true;
            try
            {
                CmbExactVersion.ItemsSource = release.SubVersions;
                if (release.SubVersions.Count > 0)
                {
                    // If index 0 is beta and index 1 exists and is stable, default to stable index 1 unless release is explicitly future/beta
                    int defaultSubIdx = (!release.IsFutureOrBeta && release.SubVersions.Count > 1 && release.SubVersions[0].IsBeta) ? 1 : 0;
                    CmbExactVersion.SelectedIndex = defaultSubIdx;
                    var sub = release.SubVersions[defaultSubIdx];
                    release.Version = sub.Version;
                    release.OsType = sub.OsType;
                    if (CmbReleaseChannel != null)
                        CmbReleaseChannel.SelectedIndex = sub.OsType == "latest" ? 1 : 0;
                    if (TxtVersionDetails != null)
                        TxtVersionDetails.Text = sub.Description;
                }
            }
            finally
            {
                _isPopulatingEditorUi = false;
            }
        }

        if (TxtVmName != null)
        {
            string verText = !string.IsNullOrEmpty(release.Version) ? $" {release.Version}" : string.Empty;
            int sameVersionCount = _allVms.Count(v => v.ReleaseId == release.Id);
            TxtVmName.Text = sameVersionCount == 0
                ? $"{release.Name}{verText} VM"
                : $"{release.Name}{verText} VM #{sameVersionCount + 1}";
        }

        if (CmbCpuProfile != null)
        {
            CmbCpuProfile.SelectedIndex = release.CpuProfile switch
            {
                "Cascadelake-Server" => 0,
                "Haswell-noTSX" => 1,
                _ => 2
            };
        }

        if (CmbSmbiosModel != null)
        {
            CmbSmbiosModel.SelectedIndex = release.SmbiosModel == "iMacPro1,1" ? 1 : 0;
        }

        RegenerateDraftSmbios(release.SmbiosModel);
        _ = LoadSubVersionsAsync(release);
    }

    private async Task LoadSubVersionsAsync(MacOSRelease release, bool forceRefresh = false)
    {
        if (CmbExactVersion == null)
            return;

        try
        {
            if (PrgLoadingVersions != null)
            {
                PrgLoadingVersions.Visibility = Visibility.Visible;
                PrgLoadingVersions.IsActive = true;
            }
            if (BtnRefreshSubVersions != null)
            {
                BtnRefreshSubVersions.IsEnabled = false;
            }

            // 1. Obter todas as subreleases conhecidas (versões finais e estáveis com builds oficiais)
            var subVersions = new List<MacOSSubVersion>();
            var knownSubs = MacOSRelease.GetKnownSubVersionsForRelease(release.Id);
            if (knownSubs != null && knownSubs.Count > 0)
            {
                subVersions.AddRange(knownSubs);
            }
            else
            {
                subVersions.Add(new MacOSSubVersion
                {
                    Version = release.Version,
                    DisplayName = $"🟢 macOS {release.Version} (Estável Oficial)",
                    OsType = "default",
                    IsBeta = false,
                    Description = "Versão padrão recomendada."
                });
            }

            release.SubVersions = subVersions;

            // Popular a interface imediatamente com todas as versões finais conhecidas
            _isPopulatingEditorUi = true;
            try
            {
                string previousVersion = (CmbExactVersion.SelectedItem as MacOSSubVersion)?.Version ?? release.Version;
                CmbExactVersion.ItemsSource = null;
                CmbExactVersion.ItemsSource = release.SubVersions;

                int matchedIdx = release.SubVersions.FindIndex(s => s.Version.Equals(previousVersion, StringComparison.OrdinalIgnoreCase));
                if (matchedIdx < 0)
                {
                    matchedIdx = (!release.IsFutureOrBeta && release.SubVersions.Count > 1 && release.SubVersions[0].IsBeta) ? 1 : 0;
                }

                CmbExactVersion.SelectedIndex = matchedIdx >= 0 ? matchedIdx : 0;
                var sub = release.SubVersions[CmbExactVersion.SelectedIndex];
                release.Version = sub.Version;
                release.OsType = sub.OsType;
                UpdateChannelSelectionForSub(sub);
                if (TxtVersionDetails != null)
                    TxtVersionDetails.Text = sub.OsType == "history" ? sub.Description : $"{sub.Description} • Canal Apple: {sub.OsType.ToUpperInvariant()}";
            }
            finally
            {
                _isPopulatingEditorUi = false;
            }

            if (_editingVm == null && TxtVmName != null)
            {
                int sameVersionCount = _allVms.Count(v => v.ReleaseId == release.Id && v.MacOSVersion == release.Version);
                TxtVmName.Text = sameVersionCount == 0
                    ? $"{release.Name} {release.Version} VM"
                    : $"{release.Name} {release.Version} VM #{sameVersionCount + 1}";
            }

            if (release.Id == "macos-26-tahoe")
            {
                try
                {
                    AddAppleVersions(await _recoveryService.GetTahoeReleaseHistoryAsync());
                }
                catch (Exception ex)
                {
                    if (ReferenceEquals(CatalogGridView.SelectedItem, release))
                        TxtVersionDetails.Text = $"Histórico do Tahoe indisponível: {ex.Message}";
                }
            }

            try
            {
                AddAppleVersions(await _recoveryService.GetAvailableSubVersionsAsync(release, forceRefresh));
            }
            catch
            {
                // O histórico e os canais de recuperação continuam disponíveis.
            }

            void AddAppleVersions(List<MacOSSubVersion> installers)
            {
                if (installers != null && installers.Count > 0)
                {
                    bool addedAny = false;
                    foreach (var inst in installers)
                    {
                        if (!subVersions.Any(s => s.Version.Equals(inst.Version, StringComparison.OrdinalIgnoreCase)
                            && s.OsType == inst.OsType && s.BuildNumber == inst.BuildNumber))
                        {
                            subVersions.Add(inst);
                            addedAny = true;
                        }
                    }

                    if (addedAny && ReferenceEquals(CatalogGridView.SelectedItem, release)
                        && ReferenceEquals(release.SubVersions, subVersions))
                    {
                        _isPopulatingEditorUi = true;
                        try
                        {
                            var currentSelected = CmbExactVersion.SelectedItem as MacOSSubVersion;
                            CmbExactVersion.ItemsSource = null;
                            CmbExactVersion.ItemsSource = release.SubVersions;
                            if (currentSelected != null)
                            {
                                CmbExactVersion.SelectedItem = currentSelected;
                            }
                        }
                        finally
                        {
                            _isPopulatingEditorUi = false;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (TxtVersionDetails != null)
            {
                TxtVersionDetails.Text = $"Consulta na Apple: {ex.Message} (Usando catálogo local).";
            }
        }
        finally
        {
            if (PrgLoadingVersions != null)
            {
                PrgLoadingVersions.IsActive = false;
                PrgLoadingVersions.Visibility = Visibility.Collapsed;
            }
            if (BtnRefreshSubVersions != null)
            {
                BtnRefreshSubVersions.IsEnabled = true;
            }
        }
    }

    private void UpdateChannelSelectionForSub(MacOSSubVersion sub)
    {
        if (CmbReleaseChannel == null)
            return;

        if (sub.OsType == "history")
            CmbReleaseChannel.SelectedIndex = -1;
        else if (sub.OsType == "installer")
            CmbReleaseChannel.SelectedIndex = 2; // Instalador Oficial Apple
        else if (sub.OsType == "latest")
            CmbReleaseChannel.SelectedIndex = 1; // Developer Seed / Beta
        else
            CmbReleaseChannel.SelectedIndex = 0; // Estável Oficial (Default)
    }

    private async void BtnRefreshSubVersions_Click(object sender, RoutedEventArgs e)
    {
        if (CatalogGridView.SelectedItem is MacOSRelease release)
        {
            await LoadSubVersionsAsync(release, forceRefresh: true);
        }
    }

    private void CmbExactVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPopulatingEditorUi || CmbExactVersion.SelectedItem is not MacOSSubVersion sub)
            return;

        if (CatalogGridView.SelectedItem is MacOSRelease release)
        {
            release.Version = sub.Version;
            release.OsType = sub.OsType;
            UpdateChannelSelectionForSub(sub);

            if (TxtVersionDetails != null)
                TxtVersionDetails.Text = sub.OsType == "history" ? sub.Description : $"{sub.Description} • Canal Apple: {sub.OsType.ToUpperInvariant()}";

            if (_editingVm == null && TxtVmName != null)
            {
                int sameVersionCount = _allVms.Count(v => v.ReleaseId == release.Id && v.MacOSVersion == sub.Version);
                TxtVmName.Text = sameVersionCount == 0
                    ? $"{release.Name} {sub.Version} VM"
                    : $"{release.Name} {sub.Version} VM #{sameVersionCount + 1}";
            }
        }
    }

    private void CmbReleaseChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPopulatingEditorUi || CatalogGridView.SelectedItem is not MacOSRelease release)
            return;

        if (CmbReleaseChannel.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            release.OsType = tag;
            if (release.SubVersions != null && release.SubVersions.Count > 0)
            {
                int targetIdx = tag switch
                {
                    "installer" => release.SubVersions.FindIndex(s => s.OsType == "installer"),
                    "latest" => release.SubVersions.FindIndex(s => s.IsBeta || s.OsType == "latest"),
                    _ => release.SubVersions.FindIndex(s => !s.IsBeta && s.OsType == "default")
                };

                if (targetIdx >= 0 && targetIdx != CmbExactVersion.SelectedIndex)
                {
                    CmbExactVersion.SelectedIndex = targetIdx;
                    return;
                }
            }

            if (TxtVersionDetails != null)
            {
                TxtVersionDetails.Text = tag switch
                {
                    "installer" => "Instalador Oficial Apple (InstallAssistant.pkg): Pacote completo de instalação obtido diretamente do CDN da Apple.",
                    "latest" => "Canal Developer / Beta ativado: O instalador buscará a build mais recente ou versão beta disponível na Apple.",
                    _ => "Canal Estável Oficial ativado: O instalador buscará a imagem padrão estável recomendada."
                };
            }
        }
    }

    private void RegenerateDraftSmbios(string model)
    {
        SmBiosGenerator.PopulateSmBios(_draftVm, model);
        if (TxtSerialNumber != null)
            TxtSerialNumber.Text = _draftVm.SerialNumber;
        if (TxtBoardSerial != null)
            TxtBoardSerial.Text = _draftVm.BoardSerialNumber;
        if (TxtSystemUuid != null)
            TxtSystemUuid.Text = _draftVm.SystemUuid;
        if (TxtRomMac != null)
            TxtRomMac.Text = _draftVm.RomMacAddress;
    }

    private void BtnRegenerateSmbios_Click(object sender, RoutedEventArgs e)
    {
        string model = CmbSmbiosModel.SelectedIndex == 1 ? "iMacPro1,1" : "MacPro7,1";
        RegenerateDraftSmbios(model);
    }

    private async void BtnBrowseCustomImage_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".dmg");
        picker.FileTypeFilter.Add(".img");
        picker.FileTypeFilter.Add(".iso");
        InitializePickerWindow(picker);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _customLocalImagePath = file.Path;
            TxtCustomImageSelected.Text = $"Imagem local selecionada: {file.Path}";
            TxtCustomImageSelected.Visibility = Visibility.Visible;
        }
    }

    private async void BtnShowAllAppleInstallers_Click(object sender, RoutedEventArgs e)
    {
        var loadingDialog = new ContentDialog
        {
            Title = "Catálogo Oficial Apple (SUCatalog)",
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock
                    {
                        Text = "Consultando catálogo de atualizações e instaladores oficiais diretamente nos servidores da Apple (swscan.apple.com)...",
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            },
            CloseButtonText = "Cancelar",
            XamlRoot = this.XamlRoot
        };

        var loadTask = _recoveryService.GetAllAppleInstallersAsync();
        var showTask = loadingDialog.ShowAsync().AsTask();

        var completed = await Task.WhenAny(loadTask, showTask);
        if (completed == showTask)
            return;

        loadingDialog.Hide();

        try
        {
            var installers = await loadTask;
            if (installers == null || installers.Count == 0)
            {
                ShowNotification("Catálogo Apple", "Nenhum instalador encontrado no catálogo da Apple.", InfoBarSeverity.Informational);
                return;
            }

            var listView = new ListView
            {
                ItemsSource = installers,
                SelectionMode = ListViewSelectionMode.Single,
                MaxHeight = 360
            };

            var listDialog = new ContentDialog
            {
                Title = $"Catálogo Apple ({installers.Count} Instaladores Encontrados)",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Selecione um pacote InstallAssistant.pkg oficial da Apple para utilizar na VM:",
                            FontSize = 12,
                            Opacity = 0.8
                        },
                        listView
                    }
                },
                PrimaryButtonText = "Utilizar Instalador Selecionado",
                CloseButtonText = "Fechar",
                XamlRoot = this.XamlRoot
            };

            var dialogResult = await listDialog.ShowAsync();
            if (dialogResult == ContentDialogResult.Primary && listView.SelectedItem is MacOSSubVersion selectedInstaller)
            {
                if (Version.TryParse(selectedInstaller.Version, out var v))
                {
                    int catalogIdx = _catalog.FindIndex(r => r.Id.Contains(v.Major.ToString()));
                    if (catalogIdx >= 0)
                    {
                        CatalogGridView.SelectedIndex = catalogIdx;
                        var rel = _catalog[catalogIdx];
                        ApplyReleaseDefaultsToEditor(rel);
                        if (!rel.SubVersions.Any(s => s.Version == selectedInstaller.Version && s.BuildNumber == selectedInstaller.BuildNumber))
                        {
                            rel.SubVersions.Insert(0, selectedInstaller);
                        }
                        CmbExactVersion.ItemsSource = null;
                        CmbExactVersion.ItemsSource = rel.SubVersions;
                        CmbExactVersion.SelectedItem = selectedInstaller;
                        UpdateChannelSelectionForSub(selectedInstaller);
                        ShowNotification("Instalador Selecionado", $"{selectedInstaller.DisplayName} configurado para a VM.", InfoBarSeverity.Success);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ShowNotification("Erro ao consultar catálogo", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void BtnBrowseSharedFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializePickerWindow(picker);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            TxtSharedFolder.Text = folder.Path;
        }
    }

    private void PopulateVmFromEditorControls(VirtualMachineConfig vm, MacOSRelease release)
    {
        int cores = ParseLeadingInt(CmbCpuCores.SelectedItem?.ToString(), _settingsService.Current.DefaultCpuCores);
        int ramGb = ParseLeadingInt(CmbRamGb.SelectedItem?.ToString(), _settingsService.Current.DefaultRamGb);
        int diskGb = ParseLeadingInt(CmbDiskGb.SelectedItem?.ToString(), _settingsService.Current.DefaultDiskGb);
        string resRaw = CmbResolution.SelectedItem?.ToString() ?? _settingsService.Current.DefaultResolution;
        string resolution = resRaw.Split(' ')[0];

        string vga = CmbVgaAdapter.SelectedIndex switch
        {
            1 => "vmware",
            2 => "virtio",
            3 => "cirrus",
            _ => "std"
        };

        string cpuProfile = CmbCpuProfile.SelectedIndex switch
        {
            0 => "Cascadelake-Server",
            1 => "Haswell-noTSX",
            _ => "Penryn"
        };

        string netCard = CmbNetworkCard.SelectedIndex == 1 ? "e1000-82545em" : "vmxnet3";
        string smbiosModel = CmbSmbiosModel.SelectedIndex == 1 ? "iMacPro1,1" : "MacPro7,1";

        vm.Name = string.IsNullOrWhiteSpace(TxtVmName.Text) ? $"{release.Name} VM" : TxtVmName.Text.Trim();
        vm.ReleaseId = release.Id;
        vm.MacOSName = release.Name;
        vm.MacOSVersion = (CmbExactVersion?.SelectedItem as MacOSSubVersion)?.Version ?? release.Version;
        vm.AccentHex = release.AccentHex;
        vm.CpuCores = cores;
        vm.RamGb = ramGb;
        vm.DiskSizeGb = diskGb;
        vm.Resolution = resolution;
        vm.VgaAdapter = vga;
        vm.CpuProfile = cpuProfile;
        vm.MountRecoveryDrive = TglMountRecovery.IsOn;
        vm.EnableAudio = TglEnableAudio.IsOn;
        vm.Fullscreen = TglFullscreen.IsOn;
        vm.NetworkCard = netCard;
        vm.SshPortForward = ParseLeadingInt(TxtVmSshPort.Text, 2222);
        vm.VncPortForward = ParseLeadingInt(TxtVmVncPort.Text, 5900);
        vm.SharedFolderPath = TxtSharedFolder.Text.Trim();
        vm.SmbiosModel = smbiosModel;
        vm.SerialNumber = string.IsNullOrWhiteSpace(TxtSerialNumber.Text) ? _draftVm.SerialNumber : TxtSerialNumber.Text.Trim();
        vm.BoardSerialNumber = string.IsNullOrWhiteSpace(TxtBoardSerial.Text) ? _draftVm.BoardSerialNumber : TxtBoardSerial.Text.Trim();
        vm.SystemUuid = string.IsNullOrWhiteSpace(TxtSystemUuid.Text) ? _draftVm.SystemUuid : TxtSystemUuid.Text.Trim();
        vm.RomMacAddress = string.IsNullOrWhiteSpace(TxtRomMac.Text) ? _draftVm.RomMacAddress : TxtRomMac.Text.Trim();
        vm.CustomQemuArgs = TxtCustomQemuArgs.Text.Trim();
        vm.OpenCoreBootArgs = TxtOpenCoreBootArgs.Text.Trim();
    }

    private async void BtnSaveEditedVm_Click(object sender, RoutedEventArgs e)
    {
        if (_editingVm == null)
            return;

        var release = (CatalogGridView.SelectedItem as MacOSRelease)
                      ?? _catalog.FirstOrDefault(c => c.Id == _editingVm.ReleaseId)
                      ?? _catalog[2];

        PopulateVmFromEditorControls(_editingVm, release);

        if (!string.IsNullOrEmpty(_customLocalImagePath) && File.Exists(_customLocalImagePath))
        {
            _editingVm.RecoveryImagePath = _customLocalImagePath;
        }

        await _storageService.SaveVmAsync(_editingVm);
        await _qemuService.WriteLaunchScriptsAsync(_editingVm);

        ShowNotification(
            "Configurações da VM Salvas",
            $"As configurações de '{_editingVm.Name}' ({_editingVm.MacOSName}) foram atualizadas com sucesso.",
            InfoBarSeverity.Success);

        LoadVmsIntoUi();
        MainNav.SelectedItem = NavItemLibrary;
        SwitchView("Library");
    }

    private async void BtnCreateVm_Click(object sender, RoutedEventArgs e)
    {
        if (CatalogGridView.SelectedItem is not MacOSRelease release)
        {
            ShowNotification("Catálogo macOS", "Selecione uma versão do macOS antes de continuar.", InfoBarSeverity.Warning);
            return;
        }

        if (CmbExactVersion.SelectedItem is MacOSSubVersion { OsType: "history" } historical &&
            !File.Exists(_customLocalImagePath))
        {
            ShowNotification($"macOS Tahoe {historical.Version}", historical.Description, InfoBarSeverity.Warning);
            return;
        }

        BtnCreateVm.IsEnabled = false;
        BtnCancelCreation.Visibility = Visibility.Visible;
        CreationProgressRing.IsActive = true;
        _creationCts = new CancellationTokenSource();

        try
        {
            var vm = new VirtualMachineConfig();
            PopulateVmFromEditorControls(vm, release);

            vm.BundleDirectory = _storageService.CreateBundlePath(vm.Name, vm.Id);
            Directory.CreateDirectory(vm.BundleDirectory);

            var progress = new Progress<DownloadProgressInfo>(info =>
            {
                CreationProgressBar.Value = info.Percentage;
                TxtProgressStage.Text = info.Stage;
                if (info.TotalBytes > 0)
                {
                    double dlMb = info.BytesDownloaded / (1024.0 * 1024.0);
                    double totalMb = info.TotalBytes / (1024.0 * 1024.0);
                    TxtProgressDetails.Text = $"{dlMb:F1} MB / {totalMb:F1} MB ({info.Percentage:F1}%) • {info.SpeedMbPerSec:F2} MB/s";
                }
            });

            // 1. Provision OpenCore + OVMF UEFI
            var (ocPath, ovmfCode, ovmfVars) = await _openCoreService.ProvisionFirmwareAndBootloaderAsync(
                vm, _storageService.CacheDirectory, progress, _creationCts.Token);
            vm.OpenCoreDiskPath = ocPath;
            vm.OvmfCodePath = ovmfCode;
            vm.OvmfVarsPath = ovmfVars;

            // 2. Download or copy macOS Recovery BaseSystem.dmg or Apple InstallAssistant package
            string dmgSourcePath;
            if (!string.IsNullOrEmpty(_customLocalImagePath) && File.Exists(_customLocalImagePath))
            {
                dmgSourcePath = _customLocalImagePath;
            }
            else
            {
                var selectedSub = CmbExactVersion?.SelectedItem as MacOSSubVersion;
                if (selectedSub != null && selectedSub.OsType == "installer" && selectedSub.IsVerifiedAvailable && !string.IsNullOrWhiteSpace(selectedSub.InstallerPackageUrl))
                {
                    TxtProgressStage.Text = $"Baixando pacote oficial Apple InstallAssistant ({selectedSub.DisplayName})...";
                    string pkgPath = await _recoveryService.DownloadInstallerPackageAsync(
                        selectedSub, _storageService.CacheDirectory, progress, _creationCts.Token);

                    // Disponibiliza o pacote baixado na pasta compartilhada da VM
                    if (string.IsNullOrWhiteSpace(vm.SharedFolderPath))
                    {
                        vm.SharedFolderPath = Path.Combine(vm.BundleDirectory, "Shared");
                        Directory.CreateDirectory(vm.SharedFolderPath);
                        string pkgDestInShared = Path.Combine(vm.SharedFolderPath, Path.GetFileName(pkgPath));
                        if (!File.Exists(pkgDestInShared))
                        {
                            try { File.Copy(pkgPath, pkgDestInShared, true); } catch { }
                        }
                    }

                    // Baixa o BaseSystem.dmg correspondente para inicialização de boot do OpenCore
                    var recoveryRelease = new MacOSRelease
                    {
                        Id = release.Id,
                        Name = release.Name,
                        Version = release.Version,
                        BoardId = release.BoardId,
                        Mlb = release.Mlb,
                        OsType = selectedSub.IsBeta ? "latest" : "default"
                    };
                    dmgSourcePath = await _recoveryService.DownloadRecoveryImageAsync(
                        recoveryRelease, _storageService.CacheDirectory, progress, _creationCts.Token);
                }
                else
                {
                    dmgSourcePath = await _recoveryService.DownloadRecoveryImageAsync(
                        release, _storageService.CacheDirectory, progress, _creationCts.Token);
                }
            }

            // 3. Create QCOW2 NVMe disk & convert BaseSystem.dmg -> BaseSystem.img
            await _qemuService.CreateOrConvertDisksAsync(vm, dmgSourcePath, progress, _creationCts.Token);

            // 4. Save VM bundle config.json
            await _storageService.SaveVmAsync(vm);

            CreationProgressBar.Value = 100;
            ShowNotification(
                "Máquina Virtual Criada",
                $"'{vm.Name}' ({vm.MacOSName} • SSH:{vm.SshPortForward} • VNC:{vm.VncPortForward}) está pronta!",
                InfoBarSeverity.Success);

            LoadVmsIntoUi();
            OpenVmEditorForCreate(switchToView: false);
            MainNav.SelectedItem = NavItemLibrary;
            SwitchView("Library");
        }
        catch (OperationCanceledException)
        {
            TxtProgressStage.Text = "Criação cancelada pelo usuário.";
            CreationProgressBar.Value = 0;
        }
        catch (Exception ex)
        {
            ShowNotification("Erro ao criar VM", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            BtnCreateVm.IsEnabled = true;
            BtnCancelCreation.Visibility = Visibility.Collapsed;
            CreationProgressRing.IsActive = false;
            _creationCts = null;
        }
    }

    private void BtnCancelCreation_Click(object sender, RoutedEventArgs e)
    {
        _creationCts?.Cancel();
    }

    private async void BtnStartVm_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        try
        {
            var proc = await _qemuService.StartVmAsync(vm);
            vm.IsRunning = true;
            vm.ProcessId = proc.Id;
            await _storageService.SaveVmAsync(vm);
            LoadVmsIntoUi();
            ShowNotification("VM Iniciada", $"{vm.Name} ({vm.MacOSName}) está rodando no QEMU (PID {proc.Id}).", InfoBarSeverity.Success);

            proc.Exited += (_, _) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    vm.IsRunning = false;
                    vm.ProcessId = null;
                    LoadVmsIntoUi();
                });
            };
        }
        catch (Exception ex)
        {
            vm.IsRunning = false;
            vm.ProcessId = null;
            LoadVmsIntoUi();
            ShowNotification("Falha ao iniciar VM", ex.Message, InfoBarSeverity.Warning);
        }
    }

    private void BtnStopVm_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        _qemuService.StopVm(vm);
        vm.IsRunning = false;
        vm.ProcessId = null;
        LoadVmsIntoUi();
        ShowNotification("VM Parada", $"O processo da máquina virtual '{vm.Name}' foi encerrado.", InfoBarSeverity.Informational);
    }

    private void BtnEditVm_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        OpenVmEditorForEdit(vm);
    }

    private async void BtnCloneVm_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        try
        {
            var clone = await _storageService.CloneVmAsync(vm);
            await _qemuService.WriteLaunchScriptsAsync(clone);
            LoadVmsIntoUi();
            ShowNotification(
                "VM Clonada",
                $"Criada '{clone.Name}' com novo Serial ({clone.SerialNumber}) e porta SSH {clone.SshPortForward}.",
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowNotification("Erro ao clonar VM", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void BtnShowQemuCmd_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        string cmd = $"qemu-system-x86_64.exe {_qemuService.BuildQemuArguments(vm)}";

        var dp = new DataPackage();
        dp.SetText(cmd);
        Clipboard.SetContent(dp);

        var dialog = new ContentDialog
        {
            Title = $"Comando QEMU — {vm.Name}",
            Content = new ScrollViewer
            {
                MaxHeight = 260,
                Content = new TextBlock
                {
                    Text = cmd,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas"),
                    IsTextSelectionEnabled = true
                }
            },
            PrimaryButtonText = "Fechar (Copiado para Área de Transferência)",
            XamlRoot = this.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void BtnShowVmSummary_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        string summary = $"{vm.Name}{Environment.NewLine}" +
            $"{vm.MacOSName} {vm.MacOSVersion}{Environment.NewLine}" +
            $"{vm.SpecsSummary}{Environment.NewLine}" +
            $"SSH {vm.SshPortForward} • Screen Sharing {vm.VncPortForward}";
        await ShowVmDiagnosticAsync("Resumo da VM", summary);
    }

    private async void BtnShowQemuLog_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        string log;
        try
        {
            string path = Path.Combine(vm.BundleDirectory, "qemu.log");
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Position = Math.Max(0, stream.Length - 64 * 1024);
            using var reader = new StreamReader(stream);
            log = await reader.ReadToEndAsync();
        }
        catch (FileNotFoundException)
        {
            log = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowNotification("Erro ao ler o log do QEMU", ex.Message, InfoBarSeverity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(log))
        {
            ShowNotification("Log do QEMU", "Esta VM ainda não tem um diagnóstico de inicialização registrado.");
            return;
        }

        await ShowVmDiagnosticAsync($"Log do QEMU — {vm.Name}", log);
    }

    private async Task ShowVmDiagnosticAsync(string title, string details)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock
                {
                    Text = details,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            },
            PrimaryButtonText = "Copiar",
            CloseButtonText = "Fechar",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var data = new DataPackage();
            data.SetText(details);
            Clipboard.SetContent(data);
        }
    }

    private void BtnOpenVmBundle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        if (Directory.Exists(vm.BundleDirectory))
        {
            OpenFolder(vm.BundleDirectory);
        }
    }

    private async void BtnDeleteVm_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not VirtualMachineConfig vm)
            return;

        var dialog = new ContentDialog
        {
            Title = "Excluir Máquina Virtual",
            Content = $"Tem certeza que deseja excluir '{vm.Name}' ({vm.MacOSName}) e todos os seus discos em:\n{vm.BundleDirectory}?",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _qemuService.StopVm(vm);
            _storageService.DeleteVm(vm);
            LoadVmsIntoUi();
            ShowNotification("VM Excluída", $"O pacote '{vm.Name}' foi removido.", InfoBarSeverity.Informational);
        }
    }

    // ==================== SETTINGS SCREEN HANDLERS ====================

    private void LoadSettingsIntoUi()
    {
        if (CmbSettingsTheme == null)
            return;

        _isLoadingSettingsUi = true;
        try
        {
            var s = _settingsService.Current;

            CmbSettingsTheme.SelectedIndex = s.Theme switch
            {
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };

            CmbSettingsBackdrop.SelectedIndex = s.BackdropType switch
            {
                "Mica" => 1,
                "Acrylic" => 2,
                _ => 0
            };

            TxtSettingsVmsDir.Text = string.IsNullOrWhiteSpace(s.CustomVmsDirectory)
                ? _storageService.VmsRootDirectory
                : s.CustomVmsDirectory;

            TxtSettingsCacheDir.Text = string.IsNullOrWhiteSpace(s.CustomCacheDirectory)
                ? _storageService.CacheDirectory
                : s.CustomCacheDirectory;

            TxtSettingsQemuSys.Text = s.CustomQemuSystemPath;
            TxtSettingsQemuImg.Text = s.CustomQemuImgPath;

            TxtSettingsDefaultCores.Text = s.DefaultCpuCores.ToString();
            TxtSettingsDefaultRam.Text = s.DefaultRamGb.ToString();
            TxtSettingsDefaultDisk.Text = s.DefaultDiskGb.ToString();
            TxtSettingsBaseSsh.Text = s.BaseSshPort.ToString();
            TxtSettingsBaseVnc.Text = s.BaseVncPort.ToString();
            TglSettingsAutoPorts.IsOn = s.AutoAssignUniquePorts;
            SelectEditorOption(CmbSettingsDefaultResolution, s.DefaultResolution);
            TglSettingsDefaultAudio.IsOn = s.DefaultEnableAudio;
            TglSettingsDefaultFullscreen.IsOn = s.DefaultFullscreen;

            RefreshCacheUsageText();
        }
        finally
        {
            _isLoadingSettingsUi = false;
        }
    }

    private void RefreshCacheUsageText()
    {
        if (TxtCacheUsageInfo == null)
            return;

        var (count, mb) = _storageService.GetCacheUsageInfo();
        TxtCacheUsageInfo.Text = $"Arquivos em cache: {count} arquivo(s) • Espaço utilizado: {mb:F1} MB em {_storageService.CacheDirectory}";
    }

    private void CmbSettingsTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettingsUi)
            return;

        if (CmbSettingsTheme.SelectedItem is ComboBoxItem item && item.Tag is string themeTag)
        {
            _settingsService.Current.Theme = themeTag;
            App.ApplyTheme(themeTag);
        }
    }

    private void CmbSettingsBackdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettingsUi)
            return;

        if (CmbSettingsBackdrop.SelectedItem is ComboBoxItem item && item.Tag is string backdropTag)
        {
            _settingsService.Current.BackdropType = backdropTag;
            App.ApplyBackdrop(backdropTag);
        }
    }

    private async void BtnBrowseVmsDir_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializePickerWindow(picker);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            TxtSettingsVmsDir.Text = folder.Path;
        }
    }

    private async void BtnBrowseCacheDir_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializePickerWindow(picker);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            TxtSettingsCacheDir.Text = folder.Path;
        }
    }

    private async void BtnBrowseQemuSys_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        InitializePickerWindow(picker);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            TxtSettingsQemuSys.Text = file.Path;
        }
    }

    private async void BtnBrowseQemuImg_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        InitializePickerWindow(picker);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            TxtSettingsQemuImg.Text = file.Path;
        }
    }

    private void BtnClearCache_Click(object sender, RoutedEventArgs e)
    {
        _storageService.ClearCache();
        RefreshCacheUsageText();
        ShowNotification("Cache Limpo", "Os arquivos temporários da pasta de cache foram removidos.", InfoBarSeverity.Success);
    }

    private async void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var s = _settingsService.Current;

        if (CmbSettingsTheme.SelectedItem is ComboBoxItem tItem && tItem.Tag is string themeTag)
            s.Theme = themeTag;

        if (CmbSettingsBackdrop.SelectedItem is ComboBoxItem bItem && bItem.Tag is string backdropTag)
            s.BackdropType = backdropTag;

        s.CustomVmsDirectory = TxtSettingsVmsDir.Text.Trim();
        s.CustomCacheDirectory = TxtSettingsCacheDir.Text.Trim();
        s.CustomQemuSystemPath = TxtSettingsQemuSys.Text.Trim();
        s.CustomQemuImgPath = TxtSettingsQemuImg.Text.Trim();

        s.DefaultCpuCores = Math.Clamp(ParseLeadingInt(TxtSettingsDefaultCores.Text, 4), 1, 64);
        s.DefaultRamGb = Math.Clamp(ParseLeadingInt(TxtSettingsDefaultRam.Text, 8), 2, 256);
        s.DefaultDiskGb = Math.Clamp(ParseLeadingInt(TxtSettingsDefaultDisk.Text, 80), 20, 4096);
        s.BaseSshPort = Math.Clamp(ParseLeadingInt(TxtSettingsBaseSsh.Text, 2222), 1024, 65000);
        s.BaseVncPort = Math.Clamp(ParseLeadingInt(TxtSettingsBaseVnc.Text, 5900), 1024, 65000);
        s.AutoAssignUniquePorts = TglSettingsAutoPorts.IsOn;
        s.DefaultResolution = CmbSettingsDefaultResolution.SelectedItem as string ?? "1920x1080";
        s.DefaultEnableAudio = TglSettingsDefaultAudio.IsOn;
        s.DefaultFullscreen = TglSettingsDefaultFullscreen.IsOn;

        await _settingsService.SaveAsync(s);
        if (_editingVm is null)
            ApplyNewVmHardwareDefaults();
        App.ApplyTheme(s.Theme);
        App.ApplyBackdrop(s.BackdropType);
        LoadVmsIntoUi();
        await RefreshDiagnosticsUiAsync();
        RefreshCacheUsageText();

        ShowNotification("Configurações Salvas", "Suas preferências de aparência, caminhos e hardware padrão foram salvas.", InfoBarSeverity.Success);
    }

    private async void BtnResetSettings_Click(object sender, RoutedEventArgs e)
    {
        await _settingsService.ResetToDefaultsAsync();
        if (_editingVm is null)
            ApplyNewVmHardwareDefaults();
        App.ApplyTheme(_settingsService.Current.Theme);
        App.ApplyBackdrop(_settingsService.Current.BackdropType);
        LoadSettingsIntoUi();
        ShowNotification("Configurações Restauradas", "Os valores padrão foram restaurados.", InfoBarSeverity.Informational);
    }

    private static void OpenFolder(string path)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private void NavOpenCache_Tapped(object sender, TappedRoutedEventArgs e)
    {
        OpenFolder(_storageService.CacheDirectory);
    }

    private void NavOpenVms_Tapped(object sender, TappedRoutedEventArgs e)
    {
        OpenFolder(_storageService.VmsRootDirectory);
    }

    private void BtnOpenCacheDir_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder(_storageService.CacheDirectory);
    }

    private void BtnOpenVmsDir_Click(object sender, RoutedEventArgs e)
    {
        OpenFolder(_storageService.VmsRootDirectory);
    }

    private async void BtnRunDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDiagnosticsUiAsync();
        ShowNotification("Diagnóstico Atualizado", "Verificação de QEMU e WHPX concluída.", InfoBarSeverity.Success);
    }

    private void BtnInstallQemuWinget_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoExit -Command \"Write-Host 'Instalando QEMU via Winget...' -ForegroundColor Cyan; winget install SoftwareFreedomConservancy.QEMU\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowNotification("Erro ao iniciar Winget", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void BtnEnableWhpx_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoExit -Command \"Write-Host 'Ativando Windows Hypervisor Platform (WHPX)...' -ForegroundColor Cyan; Enable-WindowsOptionalFeature -Online -FeatureName HypervisorPlatform -All\"",
                Verb = "runas",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowNotification("Permissão de Administrador", ex.Message, InfoBarSeverity.Warning);
        }
    }

    private static void InitializePickerWindow(object picker)
    {
#if !WINDOWS_UWP
        if (App.CurrentWindow != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
#endif
    }

    private void ApplyNewVmHardwareDefaults()
    {
        var settings = _settingsService.Current;
        SelectEditorOption(CmbCpuCores, settings.DefaultCpuCores.ToString());
        SelectEditorOption(CmbRamGb, settings.DefaultRamGb.ToString());
        SelectEditorOption(CmbDiskGb, settings.DefaultDiskGb.ToString());
        SelectEditorOption(CmbResolution, settings.DefaultResolution);
        TglEnableAudio.IsOn = settings.DefaultEnableAudio;
        TglFullscreen.IsOn = settings.DefaultFullscreen;
    }

    private static void SelectEditorOption(ComboBox comboBox, string value)
    {
        var option = comboBox.Items.OfType<string>().FirstOrDefault(item => item.Split(' ')[0] == value);
        if (option is null)
        {
            option = value;
            comboBox.Items.Add(option);
        }

        comboBox.SelectedItem = option;
    }

    private static int ParseLeadingInt(string? text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out int val) ? val : fallback;
    }
}
