using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
            }
        }

        public string? NewName
        {
            get => _newName;
            set
            {
                _newName = value;
                OnPropertyChanged(nameof(NewName));

                if (TextBox is not null && value is not null)
                    TextBox.CaretIndex = value.Length;
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

        public int StoredChoices => Choices.Count / 2;

        async void PasteItems()
        {
            if (CurrentApp.TopLevel is not null)
            {
                var Clipboard = CurrentApp.TopLevel.Clipboard;

                if (Clipboard is not null)
                {
                    var text = await Clipboard.GetTextAsync();

                    if (text is not null)
                    {
                        var items = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Order().Distinct().ToList();

                        OutputItems.Clear();
                        Undo.Clear();
                        Tiers.Clear();
                        OnPropertyChanged(nameof(UndoVisible));

                        InputItems = new ObservableCollection<string>(items);

                        GetPercentMatch();

                        StartTournament();
                    }
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

        void StartTournament()
        {

            Tiers.Add(new Tier(InputItems.ToList()));

            TierIndex = Tiers.Count - 1;

            GetTotal();
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
                var Matching = MatchingChoices.Count() / 2;

                //Ask the user whether they are sure they want to replace the choices
                var result = await MessageBoxManager.GetMessageBoxStandard("Are you sure?", "Are you sure you want to replace all choices for '" + SelectedItem + "'?\r\n\r\n" + Matching + " choices will be replaced!", MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning).ShowAsync();

                if (result == MsBox.Avalonia.Enums.ButtonResult.Yes)
                {
                    //If the user is sure, we can start the replacement process
                    //The idea is to replace all existing choices from one specific item with new choices
                    //First, we need to get a list of all choices that match the chosen item
                    var ChoicesToReplace = MatchingChoices.Where(x => x.Key.Item1 == SelectedItem).Select(x => x.Key.Item2).Distinct().ToList();

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
                        Choices.Remove((item.Key.Item2, item.Key.Item1));
                    }

                    GetNext();
                }
            }            
        }

        public string? ReplacementString => ReplacementMode && ReplacementTier is not null ?
            "Currently replacing choices for " + ReplacementItem + ". \r\n" +
            "Completed " + (ReplacementTier.CurrentPosition / 2) + " / " + (ReplacementTier.NumberOfChoices) + " choices."
            : null;

        void GetNext()
        {
            if (CurrentTier is not null)
            {
                if (ReplacementMode)
                    OnPropertyChanged(nameof(ReplacementString));
                var NextItems = CurrentTier.GetNext();
                CurrentTier.CurrentPosition += 2;
                if (NextItems.Length == 2)
                {
                    Choice1 = NextItems[0];
                    Choice2 = NextItems[1];

                    if (Choices.ContainsKey((Choice1, Choice2)))
                    {
                        if (!ReplacementMode)
                        {
                            CurrentTier.Outputs.Add(Choices[(Choice1, Choice2)]);
                            CurrentProgress++;
                            TotalProgress++;
                        }                            
                        GetNext();
                    }
                }
                else if (NextItems.Length == 1 && !ReplacementMode)
                {
                    Choice1 = null;
                    Choice2 = null;

                    CurrentTier.Outputs.Add(CurrentTier.Inputs.Last());
                    TotalProgress++;

                    GetNextTier();
                }
                else if (!ReplacementMode)
                {
                    GetNextTier();
                }
                else
                {
                    ReplacementMode = false;
                    ReplacementTier = null;
                    ReplacementItem = null;
                    DeselectItem();
                    GetNext();
                }
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

        void GetNextTier()
        {
            if (CurrentTier is not null)
            {
                if (CurrentTier.Outputs.Count > 1)
                {
                    Tiers.Add(new Tier(CurrentTier.Outputs));
                    TierIndex = Tiers.Count - 1;

                    GetNext();
                }
                else
                {
                    var item = CurrentTier.Outputs.First();

                    var number = OutputItems.Count + 1;
                    OutputItems.Add(number + ". " + item);

                    //Scroll to include the new items into view
                    //then select the original item again
                    var PreviouslySelected = OutputSelected;
                    OutputSelected = OutputItems.Count - 1;
                    OutputSelected = PreviouslySelected;

                    InputItems.Remove(item);
                    GetPercentMatch();

                    if (InputItems.Count > 0)
                        StartTournament();

                    OnPropertyChanged(nameof(OptimizeVisible));
                    OnPropertyChanged(nameof(OutputCopyVisible));
                    OnPropertyChanged(nameof(CopyVisible));
                }
            }
        }

        void GetTotal()
        {
            CurrentTotal = GetTotalForInputs(InputItems.Count);
            CurrentProgress = 0;

            if (OutputItems.Count == 0)
            {
                TotalTotal = Enumerable.Range(1, InputItems.Count).Select(x => GetTotalForInputs(x)).Sum();
                TotalProgress = 0;
            }
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
                Undo.Push(new UndoState(TierIndex, CurrentTier.CurrentPosition, Choice1, Choice2, Choice1, CurrentTotal, TotalTotal, CurrentProgress, TotalProgress, new ObservableCollection<string>(InputItems), new ObservableCollection<string>(OutputItems), CurrentTier.Outputs.ToList(), ReplacementMode, ReplacementItem, ReplacementTier));

                if (!ReplacementMode)
                {
                    CurrentTier.Outputs.Add(Choice1);
                    CurrentProgress++;
                    TotalProgress++;
                }
                    
                Choices[(Choice1, Choice2)] = Choice1;
                Choices[(Choice2, Choice1)] = Choice1;

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
                Undo.Push(new UndoState(TierIndex, CurrentTier.CurrentPosition, Choice1, Choice2, Choice2, CurrentTotal, TotalTotal, CurrentProgress, TotalProgress, new ObservableCollection<string>(InputItems), new ObservableCollection<string>(OutputItems), CurrentTier.Outputs.ToList(), ReplacementMode, ReplacementItem, ReplacementTier));

                if (!ReplacementMode)
                {
                    CurrentTier.Outputs.Add(Choice2);
                    CurrentProgress++;
                    TotalProgress++;
                }
                    
                Choices[(Choice1, Choice2)] = Choice2;
                Choices[(Choice2, Choice1)] = Choice2;

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
                        var File = Files.First().Path;

                        var Data = await ReadObjectAsync<MainViewModel>(File.LocalPath);

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
                                Choices = Data.Choices;
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
                }
            }
            catch (Exception ex)
            {
                var msg = MessageBoxManager.GetMessageBoxStandard("Error", "Error loading data: \r\n\r\n" + ex.Message);

                await msg.ShowAsync();
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
                        PositionValid = Tiers[UndoState.TierIndex].Inputs.Count > UndoState.CurrentPosition - 2;

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

                        Choices.Remove((UndoState.Choice1, UndoState.Choice2));
                        Choices.Remove((UndoState.Choice2, UndoState.Choice1));

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

        public void DeselectItem()
        {
            SelectedItem = null;

            if (Flyout is not null)
                Flyout.Hide();
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
            Task.Run(async () =>
            {
                int total = 0, match = 0;

                foreach (var item1 in InputItems)
                {
                    foreach (var item2 in InputItems.Where(x => x.CompareTo(item1) > 0))
                    {
                        total++;
                        if (Choices.ContainsKey((item1, item2)))
                            match++;
                    }
                }

                if (total == 0)
                    await Dispatcher.UIThread.InvokeAsync(() => PercentMatch = null);

                var percent = (double)match / total;

                if (total == 0)
                    await Dispatcher.UIThread.InvokeAsync(() => PercentMatch = "(0 left)");
                else
                    await Dispatcher.UIThread.InvokeAsync(() => PercentMatch = " (" + percent.ToString("P2") + " of possible choices matched - " + (total - match).ToString("N0") + " left)");
            });            
        }

        //Command Handler for optimizing schedule
        public CommandHandler OptimizeSorting => new CommandHandler(Optimize_Sorting);

        public void Optimize_Sorting()
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

            //Generate an IEnumerable which includes every output item, the original index, and the average win %
            var OutputData = CurrentOutputs.AsParallel().Select((x, i) =>
            {
                //Calculate the percentage of successes with this item in memory
                var success = Choices.Where(c => c.Key.Item1 == x && CurrentOutputs.Contains(c.Key.Item2)).Select(c => c.Value == x ? 1.0 : 0.0).Average();

                return new { Item = x, Rank = i, Success = success };
            }).OrderByDescending(x => x.Success).ThenBy(x => x.Rank).ToList();

            //Replace the existing outputs with the optimized list
            OutputItems.Clear();
            for (int i = 0; i < OutputData.Count; i++)
            {
                OutputItems.Add((i + 1) + ". " + OutputData[i].Item);
            }
        }

        bool _withNumbers = true;
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

        Timer FadeTimer = new Timer(100);
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
                if (OutputClipboardOpacity == 0 && ClipboardOpacity ==0)
                    FadeTimer.Stop();
            };
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

                    Choices.Remove((item1, item2));

                    if (item1 == OldName)
                        item1 = NewName;

                    else if (item2 == OldName)
                        item2 = NewName;

                    if (value == OldName) 
                        value = NewName;

                    Choices[(item1, item2)] = value;                    
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
    }
}
