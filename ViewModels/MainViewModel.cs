using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;

namespace TournamentWizard.ViewModels
{
    [DataContract]
    public partial class MainViewModel : ViewModelBase
    {
        [DataMember]
        ObservableCollection<string> _inputItems = new(), _outputItems = new();
        public ObservableCollection<string> InputItems
        {
            get => _inputItems;
            set
            {
                _inputItems = value;
                OnPropertyChanged(nameof(InputItems));
                OnPropertyChanged(nameof(OptimizeVisible));
                OnPropertyChanged(nameof(CopyVisible));
            }
        }

        public ObservableCollection<string> OutputItems
        {
            get => _outputItems;
            set
            {
                _outputItems = value;
                OnPropertyChanged(nameof(OutputItems));
                OnPropertyChanged(nameof(OptimizeVisible));
                OnPropertyChanged(nameof(OutputCopyVisible));
                OnPropertyChanged(nameof(CopyVisible));
            }
        }

        public CommandHandler Paste_Items => new CommandHandler(PasteItems);

        [DataMember]
        public Dictionary<(string, string), string> Choices = new();

        [DataMember]
        List<Tier> Tiers = new();

        [DataMember]
        int TierIndex = 0;
        Tier? CurrentTier => ReplacementMode ? ReplacementTier : Tiers.Count > 0 ? Tiers[TierIndex] : null;

        Stack<UndoState>
            Undo = new();

        public bool UndoVisible => Undo.Count > 0;

        public int Completed => Tiers.SelectMany(x => x.Outputs).Count();

        [DataMember]
        int _currentTotal = 0, _totalTotal = 0, _currentProgress = 0, _totalProgress = 0;

        string? _selectedItem = null, _newName = null;
        public string? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                OnPropertyChanged(nameof(DeleteVisible));

                NewName = value;
                if (TextBox is not null && value is not null)
                    TextBox.CaretIndex = value.Length;
            }
        }

        public string? NewName
        {
            get => _newName;
            set
            {
                _newName = value;
                OnPropertyChanged(nameof(NewName));


            }
        }

        public bool DeleteVisible => SelectedItem is not null;

        public int CurrentTotal
        {
            get => _currentTotal;
            set
            {
                _currentTotal = value;
                OnPropertyChanged(nameof(CurrentTotal));
                OnPropertyChanged(nameof(CurrentPercent));
            }
        }
        public int TotalTotal
        {
            get => _totalTotal;
            set
            {
                _totalTotal = value;
                OnPropertyChanged(nameof(TotalTotal));
                OnPropertyChanged(nameof(TotalPercent));
                OnPropertyChanged(nameof(ProgressOpacity));
            }
        }
        public int CurrentProgress
        {
            get => _currentProgress;
            set
            {
                _currentProgress = value;
                if (_currentProgress > _currentTotal)
                    _currentProgress = _currentTotal;
                OnPropertyChanged(nameof(CurrentProgress));
                OnPropertyChanged(nameof(CurrentPercent));
            }
        }
        public int TotalProgress
        {
            get => _totalProgress;
            set
            {
                _totalProgress = value;
                if (_totalProgress > _totalTotal)
                    _totalProgress = _totalTotal;
                OnPropertyChanged(nameof(TotalProgress));
                OnPropertyChanged(nameof(TotalPercent));
            }
        }

        public double? CurrentPercent => CurrentTotal > 0 ? (double)CurrentProgress / CurrentTotal : null;
        public double? TotalPercent => TotalTotal > 0 ? (double)TotalProgress / TotalTotal : null;

        public double ProgressOpacity => TotalTotal > 0 ? 1 : 0;

        public int StoredChoices => Choices.Count;

        bool _autoSave = true;
        public bool AutoSave
        {
            get => _autoSave;
            set
            {
                _autoSave = value;
                OnPropertyChanged(nameof(AutoSave));

                if (value)
                    AutoSaveTimer.Start();
                else
                {
                    AutoSaveTimer.Stop();

                    try
                    {
                        if (File.Exists(AutoSavePath))
                            File.Delete(AutoSavePath);
                    }
                    catch
                    {
                        //Ignore any errors that occur while deleting the file, since it's not critical if the file fails to delete for some reason
                    }


                    AutoSaveOpacity = 0;
                }

            }
        }

        int? LastSavedOutputs, LastSavedChoices;

        Timer AutoSaveTimer = new Timer(1000);

        string? AutoSavePath;

        double _autoSaveOpacity = 0;
        public double AutoSaveOpacity
        {
            get => _autoSaveOpacity;
            set
            {
                _autoSaveOpacity = value;
                OnPropertyChanged(nameof(AutoSaveOpacity));
            }
        }

        async void PasteItems()
        {
            if (CurrentApp.TopLevel is not null)
            {
                var Clipboard = CurrentApp.TopLevel.Clipboard;

                if (Clipboard is not null)
                {
                    var text = await Clipboard.TryGetTextAsync();

                    if (text is not null)
                    {
                        var items = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Order().Distinct().ToList();

                        OutputItems.Clear();
                        OnPropertyChanged(nameof(OutputCopyVisible));
                        Undo.Clear();
                        Tiers.Clear();
                        OnPropertyChanged(nameof(UndoVisible));

                        InputItems = new ObservableCollection<string>(items);

                        GetPercentMatch();

                        await StartTournament(true);
                    }

                    //Reset the AutoSave since the current save will no longer be relevant after pasting new items
                    if (LastSavedOutputs.HasValue)
                        LastSavedOutputs = null;
                }
            }
        }

        [DataMember]
        string? _choice1, _choice2;
        public string? Choice1
        {
            get => _choice1;
            set
            {
                _choice1 = value;
                OnPropertyChanged(nameof(Choice1));
                OnPropertyChanged(nameof(Choice1Visible));
            }
        }

        public string? Choice2
        {
            get => _choice2;
            set
            {
                _choice2 = value;
                OnPropertyChanged(nameof(Choice2));
                OnPropertyChanged(nameof(Choice2Visible));
            }
        }

        public bool Choice1Visible => Choice1 is not null;
        public bool Choice2Visible => Choice2 is not null;

        public bool OptimizeVisible => OutputItems.Count > 0 && InputItems.Count == 0;

        async Task StartTournament(bool first = false)
        {

            Tiers.Add(new Tier(InputItems.ToList()));

            TierIndex = Tiers.Count - 1;

            await GetTotal();

            if (first)
                GetNext();
        }

        [DataMember]
        Tier? ReplacementTier;
        [DataMember]
        bool _replacementMode = false;
        public bool ReplacementMode
        {
            get => _replacementMode;
            set
            {
                _replacementMode = value;
                OnPropertyChanged(nameof(ReplacementMode));
            }
        }

        [DataMember]
        string? _replacementItem;
        public string? ReplacementItem
        {
            get => _replacementItem;
            set
            {
                _replacementItem = value;
                OnPropertyChanged(nameof(ReplacementItem));
                OnPropertyChanged(nameof(ReplacementString));
            }
        }

        public CommandHandler Start_Replacement => new CommandHandler(StartReplacement);

        async void StartReplacement()
        {
            if (SelectedItem is not null)
            {
                //First find all matching choices
                var MatchingChoices = Choices.Where(x => x.Key.Item1 == SelectedItem || x.Key.Item2 == SelectedItem).ToList();
                var Matching = MatchingChoices.Count();// / 2;

                //Ask the user whether they are sure they want to replace the choices
                var result = await MessageBoxManager.GetMessageBoxStandard("Are you sure?", "Are you sure you want to replace all choices for '" + SelectedItem + "'?\r\n\r\n" + Matching + " choices will be replaced!", MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning).ShowAsync();

                if (result == MsBox.Avalonia.Enums.ButtonResult.Yes)
                {
                    //If the user is sure, we can start the replacement process
                    //The idea is to replace all existing choices from one specific item with new choices
                    //First, we need to get a list of all choices that match the chosen item
                    var ChoicesToReplace = MatchingChoices.Select(x => x.Key.Item1 == SelectedItem ? x.Key.Item2 : x.Key.Item1).Distinct().ToList();

                    //Choice order will be randomized so that it doesn't influence user choice
                    var r = Random.Shared;
                    var inputs = new List<string>();

                    var RandomChoices = ChoicesToReplace.OrderBy(x => r.NextDouble()).ToList();

                    foreach (var item in RandomChoices)
                    {
                        if (r.NextDouble() > 0.5)
                        {
                            inputs.Add(item);
                            inputs.Add(SelectedItem);
                        }
                        else
                        {
                            inputs.Add(SelectedItem);
                            inputs.Add(item);
                        }
                    }

                    //Reset the current position of the current tier, so we can return to it when ReplacementMode is over
                    if (CurrentTier is not null)
                        CurrentTier.CurrentPosition -= 2;

                    //Now let's create the replacement tier

                    ReplacementTier = new Tier(inputs, true);
                    ReplacementMode = true;
                    ReplacementItem = SelectedItem;

                    //Finally, we need to delete the old choices
                    foreach (var item in MatchingChoices)
                    {
                        Choices.Remove(item.Key);
                    }

                    GetNext();
                }
            }
        }

        public string? ReplacementString => ReplacementMode && ReplacementTier is not null ?
            "Currently replacing choices for " + ReplacementItem + ". \r\n" +
            "Completed " + (ReplacementTier.CurrentPosition / 2) + " / " + (ReplacementTier.NumberOfChoices) + " choices."
            : null;

        async void GetNext()
        {

            if (CurrentTier is not null)
            {
                await Task.Run(async () =>
                {
                    bool FoundNext = false;

                    string? choice1 = null, choice2 = null;
                    int currentprogress = CurrentProgress, totalprogress = TotalProgress;

                    while (!FoundNext)
                    {
                        choice1 = null;
                        choice2 = null;

                        var NextItems = CurrentTier.GetNext();
                        CurrentTier.CurrentPosition += 2;
                        if (NextItems.Length == 2)
                        {
                            choice1 = NextItems[0];
                            choice2 = NextItems[1];

                            var key = string.Compare(choice1, choice2) < 0 ? (choice1, choice2) : (choice2, choice1);

                            if (Choices.ContainsKey(key))
                            {
                                if (!ReplacementMode)
                                {
                                    CurrentTier.Outputs.Add(Choices[key]);
                                    currentprogress++;
                                    totalprogress++;
                                }
                                //GetNext();
                            }
                            else
                                FoundNext = true;
                        }
                        else if (NextItems.Length == 1 && !ReplacementMode)
                        {
                            choice1 = null;
                            choice2 = null;

                            CurrentTier.Outputs.Add(CurrentTier.Inputs.Last());
                            currentprogress++;
                            totalprogress++;

                            FoundNext = await GetNextTier();
                            if (CurrentProgress == 0)
                                currentprogress = 0;
                        }
                        else if (!ReplacementMode)
                        {
                            FoundNext = await GetNextTier();
                            if (CurrentProgress == 0)
                                currentprogress = 0;
                        }
                        else
                        {
                            ReplacementMode = false;
                            ReplacementTier = null;
                            ReplacementItem = null;
                            DeselectItem();
                            //GetNext();
                        }
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        CurrentProgress = currentprogress;
                        TotalProgress = totalprogress;
                        Choice1 = choice1;
                        Choice2 = choice2;

                        if (ReplacementMode)
                            OnPropertyChanged(nameof(ReplacementString));
                    });
                });
            }
        }

        int _outputSelected = -1;
        public int OutputSelected
        {
            get => _outputSelected;
            set
            {
                _outputSelected = value;
                OnPropertyChanged(nameof(OutputSelected));
            }
        }

        async Task<bool> GetNextTier()
        {
            if (CurrentTier is not null)
            {
                if (CurrentTier.Outputs.Count > 1)
                {
                    Tiers.Add(new Tier(CurrentTier.Outputs));
                    TierIndex = Tiers.Count - 1;

                    return false;
                }
                else
                {
                    var item = CurrentTier.Outputs.First();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var number = OutputItems.Count + 1;
                        OutputItems.Add(number + ". " + item);

                        //Scroll to include the new items into view
                        //then select the original item again
                        var PreviouslySelected = OutputSelected;
                        OutputSelected = OutputItems.Count - 1;
                        OutputSelected = PreviouslySelected;

                        InputItems.Remove(item);

                        OnPropertyChanged(nameof(OptimizeVisible));
                        OnPropertyChanged(nameof(OutputCopyVisible));
                        OnPropertyChanged(nameof(CopyVisible));
                    });

                    GetPercentMatch();

                    if (InputItems.Count > 0)
                    {
                        await StartTournament();
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        async Task GetTotal()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentTotal = GetTotalForInputs(InputItems.Count);
                CurrentProgress = 0;

                if (OutputItems.Count == 0)
                {
                    TotalTotal = Enumerable.Range(1, InputItems.Count).Select(x => GetTotalForInputs(x)).Sum();
                    TotalProgress = 0;
                }
            });
        }

        int GetTotalForInputs(int inputs)
        {
            int total = 0;
            double current = inputs;
            while (current > 1)
            {
                current = Math.Ceiling(current / 2);
                total += (int)current;
            }

            return total;
        }

        public CommandHandler Choose_1 => new CommandHandler(Choose1);
        public CommandHandler Choose_2 => new CommandHandler(Choose2);

        public void Choose1()
        {
            if (CurrentTier is not null && Choice1 is not null && Choice2 is not null)
            {
                Undo.Push(new UndoState(TierIndex, CurrentTier.CurrentPosition, Choice1, Choice2, Choice1, CurrentTotal, TotalTotal, CurrentProgress, TotalProgress, new ObservableCollection<string>(InputItems), new ObservableCollection<string>(OutputItems), CurrentTier.Outputs.ToList(), ReplacementMode, ReplacementItem, ReplacementTier is null ? null : new Tier(ReplacementTier)));

                if (!ReplacementMode)
                {
                    CurrentTier.Outputs.Add(Choice1);
                    CurrentProgress++;
                    TotalProgress++;
                }

                var key = string.Compare(Choice1, Choice2) < 0 ? (Choice1, Choice2) : (Choice2, Choice1);

                Choices[key] = Choice1;

                OnPropertyChanged(nameof(UndoVisible));
                OnPropertyChanged(nameof(StoredChoices));
                GetPercentMatch();



                //If future tiers remain from undo actions, remove them
                if (Tiers.Count > TierIndex + 1)
                    Tiers.RemoveRange(TierIndex + 1, Tiers.Count - (TierIndex + 1));

                GetNext();
            }
        }

        public void Choose2()
        {
            if (CurrentTier is not null && Choice1 is not null && Choice2 is not null)
            {
                Undo.Push(new UndoState(TierIndex, CurrentTier.CurrentPosition, Choice1, Choice2, Choice2, CurrentTotal, TotalTotal, CurrentProgress, TotalProgress, new ObservableCollection<string>(InputItems), new ObservableCollection<string>(OutputItems), CurrentTier.Outputs.ToList(), ReplacementMode, ReplacementItem, ReplacementTier is null ? null : new Tier(ReplacementTier)));

                if (!ReplacementMode)
                {
                    CurrentTier.Outputs.Add(Choice2);
                    CurrentProgress++;
                    TotalProgress++;
                }

                var key = string.Compare(Choice1, Choice2) < 0 ? (Choice1, Choice2) : (Choice2, Choice1);

                Choices[key] = Choice2;

                OnPropertyChanged(nameof(UndoVisible));
                OnPropertyChanged(nameof(StoredChoices));
                GetPercentMatch();


                //If future tiers remain from undo actions, remove them
                if (Tiers.Count > TierIndex + 1)
                    Tiers.RemoveRange(TierIndex + 1, Tiers.Count - (TierIndex + 1));

                GetNext();
            }
        }

        /// <summary>
        /// Write an object to file
        /// </summary>
        /// <typeparam name="T">Type of object to write</typeparam>
        /// <param name="FileName">File Name</param>
        /// <param name="item">The object to write</param>
        async Task WriteObjectAsync<T>(string FileName, T item)
        {
            await Task.Run(() =>
            {
                using (var writer = new FileStream(FileName, FileMode.Create))
                {
                    new DataContractSerializer(typeof(T)).WriteObject(writer, item);
                }
            });
        }

        /// <summary>
        /// Save an object to file
        /// </summary>
        /// <typeparam name="T">Type of object to save</typeparam>
        /// <param name="FileName">File name</param>
        async Task<T?> ReadObjectAsync<T>(string FileName)
        {
            var item = await Task.Run(() =>
            {
                using (var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
                {
                    return new DataContractSerializer(typeof(T)).ReadObject(fs);
                }
            });

            if (item is null) return default;

            return (T)item;
        }

        public CommandHandler Load_State => new CommandHandler(LoadState);
        async void LoadState()
        {
            try
            {
                var TopLevel = CurrentApp.TopLevel;

                if (TopLevel is not null)
                {
                    var xmlFileType = new FilePickerFileType("XML Files");
                    xmlFileType.Patterns = new[] { "*.xml" };

                    var Files = await TopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Load State",
                        AllowMultiple = false,
                        SuggestedStartLocation = await TopLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Desktop),
                        FileTypeFilter = new List<FilePickerFileType> { xmlFileType }
                    });

                    if (Files.Any())
                    {
                        var file = Files.First().Path;

                        await LoadDataFromPathAsync(file.LocalPath);


                        LastSavedOutputs = null;
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = MessageBoxManager.GetMessageBoxStandard("Error", "Error loading data: \r\n\r\n" + ex.Message);

                await msg.ShowAsync();
            }
        }

        async Task LoadDataFromPathAsync(string path)
        {
            var Data = await ReadObjectAsync<MainViewModel>(path);

            if (Data is not null)
            {
                if (Data.InputItems is not null)
                    InputItems = Data.InputItems;
                else
                    InputItems.Clear();

                if (Data.OutputItems is not null)
                    OutputItems = Data.OutputItems;
                else
                    OutputItems.Clear();

                OutputSelected = OutputItems.Count - 1;

                if (Data.Choices is not null)
                {
                    //When loading choices, we want to make sure that the keys are in the correct order (item1 < item2) to avoid any issues with lookups later on, so we check the order of the first key and reorder if necessary
                    if (Data.Choices.Where(x => string.Compare(x.Key.Item1, x.Key.Item2) > 0).Any())
                        Choices = Data.Choices.Where(x => string.Compare(x.Key.Item1, x.Key.Item2) < 0).ToDictionary(x => x.Key, x => x.Value);
                    else
                        Choices = Data.Choices;
                }
                else
                    Choices.Clear();

                OnPropertyChanged(nameof(StoredChoices));

                Tiers.Clear();

                if (Data.Tiers is not null)
                {
                    var LastItem = Data.Tiers.Last();
                    Tiers.Add(LastItem);
                }

                CurrentTotal = Data.CurrentTotal;

                TotalTotal = Data.TotalTotal;

                CurrentProgress = Data.CurrentProgress;

                TotalProgress = Data.TotalProgress;

                Choice1 = Data.Choice1;
                Choice2 = Data.Choice2;

                TierIndex = 0;

                GetPercentMatch();

                ReplacementMode = Data.ReplacementMode;
                ReplacementTier = Data.ReplacementTier;
                ReplacementItem = Data.ReplacementItem;

                if (ReplacementMode && ReplacementTier is not null)
                {
                    ReplacementTier.CurrentPosition -= 2;
                    OnPropertyChanged(nameof(ReplacementString));
                    ReplacementTier.CurrentPosition += 2;
                }
            }
        }

        public CommandHandler Save_State => new CommandHandler(SaveState);
        async void SaveState()
        {
            try
            {
                var TopLevel = CurrentApp.TopLevel;

                if (TopLevel is not null)
                {
                    var xmlFileType = new FilePickerFileType("XML Files");
                    xmlFileType.Patterns = new[] { "*.xml" };

                    var file = await TopLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Load State",
                        SuggestedStartLocation = await TopLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Desktop),
                        FileTypeChoices = new List<FilePickerFileType> { xmlFileType }
                    });

                    if (file is not null)
                    {
                        var path = file.TryGetLocalPath();

                        if (path is not null)
                        {
                            await WriteObjectAsync<MainViewModel>(path, this);

                            //Reset AutoSave if OutputItems has increased since we last autosaved

                            if (LastSavedOutputs.HasValue && OutputItems.Count > LastSavedOutputs)
                                LastSavedOutputs = OutputItems.Count;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = MessageBoxManager.GetMessageBoxStandard("Error", "Error saving data: \r\n\r\n" + ex.Message);

                await msg.ShowAsync();
            }
        }

        public CommandHandler Undo_Click => new CommandHandler(UndoChoice);

        async void UndoChoice()
        {
            if (Undo is not null && Undo.Count > 0)
            {
                var UndoState = Undo.Pop();


                if (UndoState is not null)
                {
                    bool
                        TierValid = Tiers.Count > UndoState.TierIndex,
                        PositionValid = UndoState.ReplacementMode && UndoState.ReplacementTier is not null ? UndoState.ReplacementTier.Inputs.Count > UndoState.ReplacementTier.CurrentPosition - 2 : Tiers[UndoState.TierIndex].Inputs.Count > UndoState.CurrentPosition - 2;

                    if (TierValid && PositionValid)
                    {
                        TierIndex = UndoState.TierIndex;
                        CurrentTier!.CurrentPosition = UndoState.CurrentPosition;

                        Choice1 = UndoState.Choice1;
                        Choice2 = UndoState.Choice2;

                        CurrentTotal = UndoState.CurrentTotal;
                        TotalTotal = UndoState.TotalToal;
                        CurrentProgress = UndoState.CurrentProgress;
                        TotalProgress = UndoState.TotalProgress;

                        InputItems = new ObservableCollection<string>(UndoState.InputItems);
                        OutputItems = new ObservableCollection<string>(UndoState.OutputItems);

                        CurrentTier!.Outputs = UndoState.CurrentOutputs.ToList();

                        var key = string.Compare(UndoState.Choice1, UndoState.Choice2) < 0 ? (UndoState.Choice1, UndoState.Choice2) : (UndoState.Choice2, UndoState.Choice1);

                        Choices.Remove(key);

                        OnPropertyChanged(nameof(StoredChoices));
                        GetPercentMatch();
                        if (Tiers.Count > TierIndex + 1)
                            Tiers.RemoveRange(TierIndex + 1, Tiers.Count - (TierIndex + 1));

                        ReplacementMode = UndoState.ReplacementMode;
                        ReplacementItem = UndoState.ReplacementItem;
                        ReplacementTier = UndoState.ReplacementTier;

                        if (ReplacementMode && ReplacementTier is not null)
                        {
                            ReplacementTier.CurrentPosition -= 2;
                            OnPropertyChanged(nameof(ReplacementString));
                            ReplacementTier.CurrentPosition += 2;
                        }

                        //Since we just undid an action, the previous save point may no longer be accurate, so we reset it to null to force a new save at the next opportunity
                        if (LastSavedOutputs.HasValue)
                            LastSavedOutputs = null;
                    }
                    else
                    {
                        var msg = MessageBoxManager.GetMessageBoxStandard("Error", "Undo state is invalid!");
                        await msg.ShowAsync();
                        Undo.Clear();
                    }
                }

                OnPropertyChanged(nameof(UndoVisible));
            }
        }

        public async void DeselectItem()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SelectedItem = null;

                if (Flyout is not null)
                    Flyout.Hide();
            });
        }

        public CommandHandler Delete_Click => new CommandHandler(DeleteItem);
        async void DeleteItem()
        {
            var ChoicesToRemove = Choices.Keys.Where(x => x.Item1 == SelectedItem || x.Item2 == SelectedItem).ToList();

            if (ChoicesToRemove.Count > 0)
            {
                var result = await MessageBoxManager.GetMessageBoxStandard("Are you sure?", "Are you sure you want to remove '" + SelectedItem + "' from memory?\r\n\r\n" + ChoicesToRemove.Count + " remembered choices will be removed!", MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning).ShowAsync();
                if (result == MsBox.Avalonia.Enums.ButtonResult.Yes)
                {
                    foreach (var item in ChoicesToRemove)
                        Choices.Remove(item);

                    DeselectItem();

                    OnPropertyChanged(nameof(StoredChoices));
                    GetPercentMatch();
                }
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard("Nothing to delete", "No choices were found matching '" + SelectedItem + "'.").ShowAsync();
                DeselectItem();
            }
        }

        string? _percentMatch;
        public string? PercentMatch
        {
            get => _percentMatch;
            set
            {
                _percentMatch = value;
                OnPropertyChanged(nameof(PercentMatch));
            }
        }

        void GetPercentMatch()
        {
            Task.Run(() =>
            {
                int total = 0, match = 0;

                foreach (var item1 in InputItems)
                {
                    foreach (var item2 in InputItems.Where(x => x.CompareTo(item1) > 0))
                    {
                        total++;
                        //var key = string.Compare(item1, item2) < 0 ? (item1, item2) : (item2, item1);
                        if (Choices.ContainsKey((item1, item2)))
                            match++;
                    }
                }

                if (total == 0)
                    Dispatcher.UIThread.Post(() => PercentMatch = null);

                var percent = (double)match / total;

                if (total == 0)
                    Dispatcher.UIThread.Post(() => PercentMatch = "(0 left)");
                else
                    Dispatcher.UIThread.Post(() => PercentMatch = " (" + percent.ToString("P2") + " of possible choices matched - " + (total - match).ToString("N0") + " left)");
            });
        }

        //Command Handler for optimizing schedule
        public CommandHandler OptimizeSorting => new CommandHandler(Optimize_Simulation);

        public async void Optimize_Sorting()
        {
            //First, gather all of the output items without their numbers
            var CurrentOutputs = new List<string>();

            int index = 0;

            foreach (var item in OutputItems)
            {
                //Find where the decimal is
                index = item.IndexOf(".");

                //Get substring
                index += 2;
                var CurrentItem = item.Substring(index, item.Length - index);

                CurrentOutputs.Add(CurrentItem);
            }


            //OnPropertyChanged(nameof(OptimizeVisible));

            //ButtonsEnabled = false;

            await Task.Run(async () =>
            {
                //Look through all remaining options and sort by chance of success
                var SortedItems = CurrentOutputs.AsParallel().Select((x, i) =>
                {
                    //Calculate the percentage of successes with this item in memory
                    var matchups = Choices.Where(c => (c.Key.Item1 == x && CurrentOutputs.Contains(c.Key.Item2)) || (c.Key.Item2 == x && CurrentOutputs.Contains(c.Key.Item1))).Select(c => c.Value == x ? 1.0 : 0.0);

                    var success = matchups.Any() ? matchups.Average() : 0.5; //If no matchups exist, use 0.5 to represent random chance, and use previous rank as a fallback 
                    return new { Item = x, Rank = i, Success = success };
                }).OrderByDescending(x => x.Success).ThenBy(x => x.Rank).ToList();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    OutputItems.Clear();

                    //Add the sorted items to the output list
                    for (int i = 0; i < SortedItems.Count; i++)
                        OutputItems.Add((i + 1) + ". " + SortedItems[i].Item);

                    OutputSelected = 0;
                    OutputSelected = -1;
                });
            });

            //OnPropertyChanged(nameof(OptimizeVisible));
            //ButtonsEnabled = true;
            OptimizeOpacity = 1;
            FadeOptimizeTimer.Start();

            //OptimizeProgress = OriginalTotal;

            //OptimizeTimer.Stop();
            //OptimizeTimer.Dispose();

            ////Restore the original Total Progress Bar values
            //TotalTotal = OriginalTotal;
            //TotalProgress = OriginalTotal;
            //

            //Reset the AutoSave since the current save may no longer be accurate after optimizing, so we force a new save at the next opportunity

            LastSavedOutputs = null;
        }

        bool _buttonsEnabled = true;
        public bool ButtonsEnabled
        {
            get => _buttonsEnabled;
            set
            {
                _buttonsEnabled = value;
                OnPropertyChanged(nameof(ButtonsEnabled));
            }
        }

        bool _withNumbers = false;
        public bool WithNumbers
        {
            get => _withNumbers;
            set
            {
                _withNumbers = value;
                OnPropertyChanged(nameof(WithNumbers));
            }
        }

        double _outputClipboardOpacity = 0;
        public double OutputClipboardOpacity
        {
            get => _outputClipboardOpacity;
            set
            {
                _outputClipboardOpacity = value;
                OnPropertyChanged(nameof(OutputClipboardOpacity));
            }
        }

        double _clipboardOpacity = 0;
        public double ClipboardOpacity
        {
            get => _clipboardOpacity;
            set
            {
                _clipboardOpacity = value;
                OnPropertyChanged(nameof(ClipboardOpacity));
            }
        }

        public CommandHandler CopyOutputs => new CommandHandler(async () =>
        {
            if (CurrentApp.TopLevel is not null && CurrentApp.TopLevel.Clipboard is not null)
            {
                string clipboard = "";
                if (WithNumbers)
                {
                    foreach (var item in OutputItems)
                        clipboard += string.IsNullOrEmpty(clipboard) ? item : "\r\n" + item;
                }
                else
                {
                    int index = 0;
                    foreach (var item in OutputItems)
                    {
                        //Find where the decimal is
                        index = item.IndexOf(".");

                        //Get substring
                        index += 2;
                        var CurrentItem = item.Substring(index, item.Length - index);

                        clipboard += string.IsNullOrEmpty(clipboard) ? CurrentItem : "\r\n" + CurrentItem;
                    }
                }

                await CurrentApp.TopLevel.Clipboard.SetTextAsync(clipboard);

                OutputClipboardOpacity = 1;
                FadeTimer.Start();
            }
        });

        Timer FadeTimer = new Timer(100), FadeOptimizeTimer = new Timer(100);
        public MainViewModel()
        {
            FadeTimer.Elapsed += async (s, e) =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ClipboardOpacity -= 0.05;
                    OutputClipboardOpacity -= 0.05;

                    if (ClipboardOpacity < 0)
                        ClipboardOpacity = 0;

                    if (OutputClipboardOpacity < 0)
                        OutputClipboardOpacity = 0;
                });
                if (OutputClipboardOpacity == 0 && ClipboardOpacity == 0)
                    FadeTimer.Stop();
            };

            FadeOptimizeTimer.Elapsed += async (s, e) =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    OptimizeOpacity -= 0.05;
                    if (OptimizeOpacity < 0)
                        OptimizeOpacity = 0;
                });
                if (OptimizeOpacity == 0)
                    FadeOptimizeTimer.Stop();
            };

            //AUTO SAVE SETUP

            //Define the autosave folder path
            var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TournamentWizard");

            //Create the folder if it doesn't exist
            Directory.CreateDirectory(folder);

            //Set AutoSave path
            AutoSavePath = System.IO.Path.Combine(folder, "autosave.xml");

            //Check if an autosave file exists, and if so, load it
            if (File.Exists(AutoSavePath))
            {
                Task.Run(async () => await LoadDataFromPathAsync(AutoSavePath));
            }

            //Set up a timer to check if autosaving is necessary, every 1 second
            AutoSaveTimer.Elapsed += async (s, e) =>
            {
                bool SaveNeeded = LastSavedOutputs.HasValue && LastSavedChoices.HasValue ?
                    (OutputItems.Count / 5) > (LastSavedOutputs.Value / 5) || (Choices.Count - LastSavedChoices > 24) : //Save every 5 new items or 25 choices, whichever comes first
                    true; //If we haven't saved before, we want to save immediately

                if (SaveNeeded)
                {
                    AutoSaveOpacity = 1;

                    try
                    {
                        await WriteObjectAsync<MainViewModel>(AutoSavePath, this);

                        LastSavedOutputs = OutputItems.Count;
                        LastSavedChoices = Choices.Count;
                    }
                    catch
                    {
                        //If saving fails, we don't want the app to crash, so we just ignore the error and try again at the next save point
                    }
                }
                else
                    AutoSaveOpacity = 0;
            };

            if (AutoSave)
                AutoSaveTimer.Start();
        }

        double _optimizeOpacity = 0;
        public double OptimizeOpacity
        {
            get => _optimizeOpacity;
            set
            {
                _optimizeOpacity = value;
                OnPropertyChanged(nameof(OptimizeOpacity));
            }
        }

        public bool OutputCopyVisible => OutputItems.Count > 0;

        public bool CopyVisible => InputItems.Count > 0 || OutputItems.Count > 0;

        public CommandHandler CopyAllItems => new CommandHandler(async () =>
        {
            if (CurrentApp.TopLevel is not null && CurrentApp.TopLevel.Clipboard is not null)
            {
                var AllItems = InputItems.ToList();

                if (OutputItems.Any())
                {
                    int index = 0;
                    foreach (var item in OutputItems)
                    {
                        //Find where the decimal is
                        index = item.IndexOf(".");

                        //Get substring
                        index += 2;
                        var CurrentItem = item.Substring(index, item.Length - index);

                        AllItems.Add(CurrentItem);
                    }

                    AllItems.Sort();
                }

                string clipboard = "";

                foreach (var item in AllItems)
                    clipboard += string.IsNullOrEmpty(clipboard) ? item : "\r\n" + item;

                await CurrentApp.TopLevel.Clipboard.SetTextAsync(clipboard);

                ClipboardOpacity = 1;
                FadeTimer.Start();
            }
        });

        public FlyoutBase? Flyout;

        public TextBox? TextBox;

        public CommandHandler RenameItem => new CommandHandler(async () =>
        {
            var OldName = SelectedItem;

            if (OldName is not null && NewName is not null)
            {
                var matches = Choices.Where(x => x.Key.Item1 == OldName || x.Key.Item2 == OldName).ToList();

                foreach (var item in matches)
                {
                    var item1 = item.Key.Item1;
                    var item2 = item.Key.Item2;
                    var value = item.Value;

                    Choices.Remove(item.Key);

                    if (item1 == OldName)
                        item1 = NewName;

                    else if (item2 == OldName)
                        item2 = NewName;

                    if (value == OldName)
                        value = NewName;

                    var key = string.Compare(item1, item2) < 0 ? (item1, item2) : (item2, item1);

                    Choices[key] = value;
                }

                foreach (var tier in Tiers)
                {
                    for (int i = 0; i < tier.Inputs.Count; i++)
                        if (tier.Inputs[i] == OldName)
                            tier.Inputs[i] = NewName;

                    for (int i = 0; i < tier.Outputs.Count; i++)
                        if (tier.Outputs[i] == OldName)
                            tier.Outputs[i] = NewName;
                }

                if (ReplacementTier is not null)
                {
                    for (int i = 0; i < ReplacementTier.Inputs.Count; i++)
                        if (ReplacementTier.Inputs[i] == OldName)
                            ReplacementTier.Inputs[i] = NewName;

                    for (int i = 0; i < ReplacementTier.Outputs.Count; i++)
                        if (ReplacementTier.Outputs[i] == OldName)
                            ReplacementTier.Outputs[i] = NewName;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Choice1 == OldName)
                        Choice1 = NewName;

                    if (Choice2 == OldName)
                        Choice2 = NewName;

                    InputItems = new ObservableCollection<string>(InputItems.Select(x => x == OldName ? NewName : x).Order());

                    if (Flyout is not null)
                        Flyout.Hide();
                });
            }
        });

        async void Optimize_Simulation()
        {
            //First, gather all of the output items without their numbers
            var CurrentOutputs = new List<string>();

            int index = 0;

            foreach (var item in OutputItems)
            {
                //Find where the decimal is
                index = item.IndexOf(".");

                //Get substring
                index += 2;
                var CurrentItem = item.Substring(index, item.Length - index);

                CurrentOutputs.Add(CurrentItem);
            }

            List<string> NewOutputs = new();

            //Set up a timer to check for new outputs every 100ms, and add them to the output list as they come in, so the user can see the results as they are calculated instead of waiting for the entire simulation to finish

            bool updatedyet = false;

            void UpdateOutputs()
            {
                if (!updatedyet)
                {
                    OutputItems.Clear();
                    updatedyet = true;
                }


                if (NewOutputs.Count > OutputItems.Count)
                {
                    //Add the missing items
                    for (int i = OutputItems.Count; i < NewOutputs.Count; i++)
                        OutputItems.Add(NewOutputs[i]);
                }
            }

            DispatcherTimer Updater = new();
            Updater.Interval = TimeSpan.FromMilliseconds(100);


            Updater.Tick += (s, e) =>
            {
                UpdateOutputs();
            };

            Updater.Start();

            await Task.Run(async () =>
            {
                //Define array to represent all possible matchups
                var Matchups = new int[CurrentOutputs.Count, CurrentOutputs.Count];

                var AllIndexes = Enumerable.Range(0, CurrentOutputs.Count).Select(x => Enumerable.Range(0, CurrentOutputs.Count).Select(y => (x, y))).SelectMany(x => x);

                Parallel.ForEach(AllIndexes, index =>
                {
                    var item1 = CurrentOutputs[index.x];
                    var item2 = CurrentOutputs[index.y];
                    var key = string.Compare(item1, item2) < 0 ? (item1, item2) : (item2, item1);
                    if (Choices.ContainsKey(key))
                    {
                        if (Choices[key] == item1)
                            Matchups[index.x, index.y] = 1; //item1 beats item2
                        else
                            Matchups[index.x, index.y] = 0; //item2 beats item1
                    }
                    else
                        Matchups[index.x, index.y] = -1; //no matchup
                });
                //Now we have a matrix of all possible matchups, we can simulate the tournament using this matrix to determine the optimal sorting

                HashSet<int> Winners = new();

                var AllItems = Enumerable.Range(0, CurrentOutputs.Count).ToList();

                int CurrentLoser = -1;
                double CurrentWinRate = 2, CurrentGlobalWinRate = 2;

                List<int> CurrentItems = AllItems.ToList();

                int[]
                    MasterWins = new int[CurrentOutputs.Count],
                    MasterPlays = new int[CurrentOutputs.Count],
                    ActiveWins = new int[CurrentOutputs.Count],
                    ActivePlays = new int[CurrentOutputs.Count];

                double[] GlobalWinRates = new double[CurrentOutputs.Count];

                Parallel.ForEach(CurrentItems, x =>
                {
                    foreach (var y in CurrentItems)
                    {
                        if (x == y) continue;
                        if (Matchups[x, y] == 1) { MasterWins[x]++; MasterPlays[x]++; }
                        else if (Matchups[x, y] == 0) { MasterPlays[x]++; }
                    }

                    // GlobalWinRates NEVER changes, so we calculate it once here and keep it forever
                    GlobalWinRates[x] = MasterPlays[x] > 0 ? (double)MasterWins[x] / MasterPlays[x] : 0.5;
                });

                //For manually inspecting the win rates and matchups during development, this LINQ query generates a sorted list of items with their global win rates, sorted from highest to lowest win rate
                //var SortedWins = Enumerable.Range(0, CurrentOutputs.Count).Select(x => new { Item = CurrentOutputs[x], WinRate = GlobalWinRates[x] }).OrderByDescending(x => x.WinRate).ToList();

                while (Winners.Count < CurrentOutputs.Count)
                {
                    // 1. Give everyone a running tally for this round
                    Array.Copy(MasterWins, ActiveWins, CurrentOutputs.Count);
                    Array.Copy(MasterPlays, ActivePlays, CurrentOutputs.Count);

                    while (CurrentItems.Count > 1)
                    {
                        foreach (var x in CurrentItems)
                        {
                            // O(1) instant lookup instead of a nested loop!
                            double WinRate = ActivePlays[x] > 0 ? (double)ActiveWins[x] / ActivePlays[x] : 0.5;
                            double GlobalWinRate = GlobalWinRates[x];

                            if (WinRate > CurrentWinRate) continue;

                            if (WinRate < CurrentWinRate ||
                                GlobalWinRate < CurrentGlobalWinRate ||
                                (GlobalWinRate == CurrentGlobalWinRate && x > CurrentLoser))
                            {
                                CurrentLoser = x;
                                CurrentWinRate = WinRate;
                                CurrentGlobalWinRate = GlobalWinRate;
                            }
                        }

                        // 2. Remove the loser from the pool
                        CurrentItems.Remove(CurrentLoser);

                        // 3. THE SPEED TRICK: Just update the tallies of the survivors!
                        foreach (var survivor in CurrentItems)
                        {
                            if (Matchups[survivor, CurrentLoser] == 1) { ActiveWins[survivor]--; ActivePlays[survivor]--; }
                            else if (Matchups[survivor, CurrentLoser] == 0) { ActivePlays[survivor]--; }
                        }

                        CurrentLoser = -1;
                        CurrentWinRate = 2;
                        CurrentGlobalWinRate = 2;
                    }

                    //while (CurrentItems.Count > 1)
                    //{
                    //    //Iterate through all possibilities, calculating win rate and choosing the worst performing item each time to eliminate, until only one item remains
                    //    foreach (var x in CurrentItems)
                    //    {
                    //        int Wins = 0, Losses = 0;

                    //        foreach (var y in CurrentItems)
                    //        {
                    //            if (x != y)
                    //            {
                    //                if (Matchups[x, y] == 1)
                    //                    Wins++;
                    //                else if (Matchups[x, y] == 0)
                    //                    Losses++;
                    //            }
                    //        }

                    //        double 
                    //            WinRate = Losses + Wins > 0 ? (double)Wins / (Wins + Losses) : 0.5,
                    //            GlobalWinRate = GlobalWinRates[x];

                    //        if (WinRate > CurrentWinRate)
                    //        {
                    //            continue;
                    //        }

                    //        bool NewLoser =
                    //            WinRate < CurrentWinRate || //Primary sort by win rate, lowest first
                    //            GlobalWinRate < CurrentGlobalWinRate || //Secondary sort by global win rate, lowest first
                    //            (GlobalWinRate == CurrentGlobalWinRate && x > CurrentLoser); //Tertiary sort by previous rank, lowest first

                    //        if (NewLoser)
                    //        {
                    //            CurrentLoser = x;
                    //            CurrentWinRate = WinRate;
                    //            CurrentGlobalWinRate = GlobalWinRates[x];
                    //        }
                    //    }

                    //    //Remove the current loser from the current items list, so we don't consider it in future iterations
                    //    CurrentItems.Remove(CurrentLoser);

                    //    //Reset the current loser and win rate for the next iteration
                    //    CurrentLoser = -1;
                    //    CurrentWinRate = 2;
                    //    CurrentGlobalWinRate = 2;
                    //}



                    //Once only one item remains, we declare it the winner and repeat the process until all items have been declared winners and we have an optimal sorting
                    int Winner = CurrentItems[0];
                    Winners.Add(Winner);
                    NewOutputs.Add(Winners.Count + ". " + CurrentOutputs[Winner]);

                    // 3. THE SPEED TRICK: Just update the tallies of the survivors!
                    foreach (var survivor in CurrentItems)
                    {
                        if (Matchups[survivor, Winner] == 1) { ActiveWins[survivor]--; ActivePlays[survivor]--; }
                        else if (Matchups[survivor, Winner] == 0) { ActivePlays[survivor]--; }
                    }

                    //Filter the current items to only those that haven't been eliminated or declared winners
                    CurrentItems = AllItems.Except(Winners).ToList();
                }
            });

            Updater.Stop();

            UpdateOutputs();

            OutputSelected = 0;
            OutputSelected = -1;

            OptimizeOpacity = 1;
            FadeOptimizeTimer.Start();

            LastSavedOutputs = null;
        }

        public CommandHandler ClearState => new CommandHandler(() =>
        {
            Choices.Clear();
            InputItems.Clear();
            OutputItems.Clear();
            Tiers.Clear();
            CurrentTotal = 0;
            TotalTotal = 0;
            CurrentProgress = 0;
            TotalProgress = 0;
            Choice1 = null;
            Choice2 = null;
            TierIndex = 0;
            ReplacementMode = false;
            ReplacementTier = null;


            GetPercentMatch();
            OnPropertyChanged(nameof(StoredChoices));
            OnPropertyChanged(nameof(PercentMatch));
        });
    }
}
