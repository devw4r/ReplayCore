using System;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Collections;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using SniffBrowser.Core;
using SniffBrowser.Events;
using BrightIdeasSoftware;
using SniffBrowser.Network;
using SniffBrowser.Controls;

namespace SniffBrowser
{
    public partial class FormSniffBrowser : Form
    {
        private NetworkClient NetworkClient;
        private FormConnectionDialog ConnectDlg;
        private readonly ImageList ImageList = new ImageList { ImageSize = new Size(64, 64) };
        private readonly CancellationTokenSource NetworkJobsCancellationToken = new CancellationTokenSource();
        private readonly BlockingCollection<byte[]> PendingNetworkJobs = new BlockingCollection<byte[]>();
        private readonly System.Windows.Forms.Timer FilteringTimer = new System.Windows.Forms.Timer();
        private static volatile bool IsClosing = false;

        private uint TimeRangeMinFilter = 0;
        private uint TimeRangeMaxFilter = 0;
        private int SelectedTimeType = 0;
        private int SelectedTimeDisplay = 0;
        private RowColorType SelectedRowColor = RowColorType.Alternating;

        public FormSniffBrowser()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitializeImageList();
            InitializeControls();
            InitializeEventsList();
            IntializeFilterLists();
            InitializeSniffedEventTypeFilterList();
        }

        private void FormSniffBrowser_Shown(object sender, EventArgs e)
        {
            ConnectToReplayCore();
        }

        private void InitializeControls()
        {
            this.MinimumSize = this.Size;

            cmbEventTypes.SelectedIndex = 0;
            cmbTimeType.SelectedIndex = 0;
            cmbTimeDisplay.SelectedIndex = 0;
            cmbRowColors.SelectedIndex = 1;

            FilteringTimer.Tick += FilteringTimer_Tick;
            FilteringTimer.Interval = 700;
            FilteringTimer.Stop();

            eventsListCtxMenu.Items.Add("Copy Event Time", null, OnCopyEventTime);
            eventsListCtxMenu.Items.Add("Copy Event Source", null, OnCopyEventSource);
            eventsListCtxMenu.Items.Add("Copy Event Target", null, OnCopyEventTarget);
            eventsListCtxMenu.Items.Add("-");
            eventsListCtxMenu.Items.Add("Remove This Row", null, OnRemoveRow);
            eventsListCtxMenu.Items.Add("Remove This Type", null, OnRemoveType);
            eventsListCtxMenu.Items.Add("Remove This Source", null, OnRemoveSource);
            eventsListCtxMenu.Items.Add("Remove This Target", null, OnRemoveTarget);

            ProcessPendingNetworkJobs();
        }

        private void InitializeImageList()
        {
            ImageList.Images.AddRange(new List<Bitmap>() {
                Properties.Resources.placeholder,
                Properties.Resources.weather_update,
                Properties.Resources.world_text,
                Properties.Resources.world_state_update,
                Properties.Resources.world_object_create1,
                Properties.Resources.world_object_create2,
                Properties.Resources.world_object_destroy,
                Properties.Resources.unit_attack_log,
                Properties.Resources.unit_attack_start,
                Properties.Resources.unit_attack_stop,
                Properties.Resources.unit_emote,
                Properties.Resources.unit_clientside_movement,
                Properties.Resources.unit_serverside_movement,
                Properties.Resources.unit_update_entry,
                Properties.Resources.unit_update_scale,
                Properties.Resources.unit_update_displayid,
                Properties.Resources.unit_update_mount,
                Properties.Resources.unit_update_faction,
                Properties.Resources.unit_update_level,
                Properties.Resources.unit_update_aurastate,
                Properties.Resources.unit_update_emotestate,
                Properties.Resources.unit_update_stand_state,
                Properties.Resources.unit_update_vis_flags,
                Properties.Resources.unit_update_sheath_state,
                Properties.Resources.unit_update_shapeshift_form,
                Properties.Resources.unit_update_npc_flags,
                Properties.Resources.unit_update_unit_flags,
                Properties.Resources.unit_update_dynamic_flags,
                Properties.Resources.unit_update_health,
                Properties.Resources.unit_update_mana,
                Properties.Resources.unit_update_bounding_radius,
                Properties.Resources.unit_update_combat_reach,
                Properties.Resources.unit_update_main_hand_attack_time,
                Properties.Resources.unit_update_off_hand_attack_time,
                Properties.Resources.unit_update_channel_spell,
                Properties.Resources.unit_update_guid_value,
                Properties.Resources.unit_update_speed,
                Properties.Resources.unit_update_auras,
                Properties.Resources.creature_text,
                Properties.Resources.unit_threat_clear,
                Properties.Resources.unit_threat_update,
                Properties.Resources.creature_equipment_update,
                Properties.Resources.player_chat,
                Properties.Resources.player_equipment_update,
                Properties.Resources.gameobject_custom_anim,
                Properties.Resources.gameobject_despawn_anim,
                Properties.Resources.gameobject_update_flags,
                Properties.Resources.gameobject_update_state,
                Properties.Resources.gameobject_update_artkit,
                Properties.Resources.gameobject_update_dynamic_flags,
                Properties.Resources.gameobject_update_path_progress,
                Properties.Resources.play_music,
                Properties.Resources.play_sound,
                Properties.Resources.play_spell_visual_kit,
                Properties.Resources.spell_cast_failed,
                Properties.Resources.spell_cast_start,
                Properties.Resources.spell_cast_go,
                Properties.Resources.client_quest_accept,
                Properties.Resources.client_quest_complete,
                Properties.Resources.client_creature_interact,
                Properties.Resources.client_gameobject_use,
                Properties.Resources.client_item_use,
                Properties.Resources.client_reclaim_corpse,
                Properties.Resources.client_release_spirit,
                Properties.Resources.quest_update_complete,
                Properties.Resources.quest_update_failed,
                Properties.Resources.xp_gain,
                Properties.Resources.faction_standing_update,
                Properties.Resources.login,
                Properties.Resources.logout,
                Properties.Resources.cinematic_begin,
                Properties.Resources.cinematic_end,
             }.ToArray());
        }

        private void InitializeEventsList()
        {
            eventsListView.SmallImageList = ImageList;
            eventsListView.FullRowSelect = true;
            eventsListView.ShowGroups = false;
            eventsListView.AllowColumnReorder = false;
            eventsListView.EmptyListMsg = "No events or nothing filtered.";
            eventsListView.RowHeight = 64;
            eventsListView.AlternateRowBackColor = Colors.RowColor_Grey;
            eventsListView.UseAlternatingBackColors = true;
            eventsListView.UseFiltering = true;
            eventsListView.FormatRow += EventsListView_FormatRow;
            eventsListView.ContextMenuStrip = eventsListCtxMenu;

            OLVColumn EventCol = new OLVColumn("Event", "Event")
            {
                AspectGetter = delegate (object o)
                {
                    return null;
                },

                ImageGetter = delegate (object o)
                {
                    if (!(o is SniffedEvent sEvent) || !DataHolder.TryGetImageIndexForEventType((uint)sEvent.EventType, out int imgIndx))
                        return null;
                    return imgIndx;
                },

                Sortable = false,
                UseFiltering = false,
                MinimumWidth = 68,
                MaximumWidth = 68
            };

            OLVColumn TimeCol = new OLVColumn("Time", "Time")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is SniffedEvent sEvent))
                        return null;
                    return sEvent.FormatTimeString(SelectedTimeType, SelectedTimeDisplay);
                },

                Sortable = false,
                UseFiltering = true
            };

            OLVColumn DescriptionCol = new OLVColumn("Description", "Description")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is SniffedEvent sEvent))
                        return null;
                    return sEvent.ShortDescription;
                },

                Sortable = false,
                FillsFreeSpace = true,
                UseFiltering = true
            };

            eventsListView.Columns.Add(EventCol);
            eventsListView.Columns.Add(TimeCol);
            eventsListView.Columns.Add(DescriptionCol);
        }

        private void InitializeSniffedEventTypeFilterList()
        {
            EventTypeFilterListView.ShowGroups = false;
            EventTypeFilterListView.CheckBoxes = true;
            EventTypeFilterListView.ShowItemToolTips = true;
            EventTypeFilterListView.FullRowSelect = true;
            EventTypeFilterListView.UseFiltering = true;

            EventTypeFilterListView.BooleanCheckStateGetter = delegate (object o)
            {
                if (!(o is SniffedEventTypeFilterEntry sEventType))
                    return false;

                return sEventType.Enabled;
            };

            EventTypeFilterListView.BooleanCheckStatePutter = delegate (object o, bool newValue)
            {
                if (!(ObjectFiltersListView.SelectedObject is Filter filter))
                    return false;

                if (!(o is SniffedEventTypeFilterEntry sEventType))
                    return false;

                if (newValue && filter.EnableSniffedEventType(sEventType.FilterType))
                    EnqueueFiltering();
                else if (!newValue && filter.DisableSniffedEventType(sEventType.FilterType))
                    EnqueueFiltering();

                return newValue;
            };

            OLVColumn StateCol = new OLVColumn("On", "On")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is SniffedEventTypeFilterEntry sEventType))
                        return false;

                    return sEventType.Enabled;
                },

                TextAlign = HorizontalAlignment.Left,
                IsEditable = true,
                UseFiltering = true,
                MinimumWidth = 27,
                MaximumWidth = 27,
                CheckBoxes = true,
                Sortable = false,
            };

            OLVColumn NameCol = new OLVColumn("Name", "Name")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is SniffedEventTypeFilterEntry sEventType))
                        return false;

                    if (DataHolder.TryGetEventTypeEntryById((uint)sEventType.FilterType, out var eventType))
                        return eventType.EventName;

                    return sEventType.FilterType.ToString().Replace("SE_", string.Empty);
                },

                UseFiltering = true,
                IsEditable = false,
                Sortable = false,
                FillsFreeSpace = true
            };

            EventTypeFilterListView.Columns.Add(StateCol);
            EventTypeFilterListView.Columns.Add(NameCol);
        }

        private void IntializeFilterLists()
        {
            ObjectFiltersListView.FullRowSelect = true;
            ObjectFiltersListView.ShowGroups = false;
            ObjectFiltersListView.AllowColumnReorder = false;
            ObjectFiltersListView.AlternateRowBackColor = Colors.RowColor_Grey;
            ObjectFiltersListView.UseAlternatingBackColors = true;
            ObjectFiltersListView.GridLines = true;
            ObjectFiltersListView.CheckBoxes = true;
            ObjectFiltersListView.ShowItemToolTips = true;

            ObjectFiltersListView.BooleanCheckStateGetter = delegate (object o)
            {
                if (!(o is Filter filter))
                    return false;

                return filter.Enabled;
            };

            ObjectFiltersListView.BooleanCheckStatePutter = delegate (object o, bool newValue)
            {
                if (!(o is Filter filter))
                    return false;

                if (filter.Enabled != newValue)
                {
                    filter.Enabled = newValue;
                    EnqueueFiltering();
                }

                return newValue;
            };

            OLVColumn StateCol = new OLVColumn("On", "On")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is Filter filter))
                        return false;

                    return filter.Enabled;
                },

                TextAlign = HorizontalAlignment.Center,
                IsEditable = true,
                MinimumWidth = 27,
                MaximumWidth = 27,
                CheckBoxes = true,
                Sortable = false,
            };

            OLVColumn NameCol = new OLVColumn("Name", "Name")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is Filter filter))
                        return null;

                    if (filter.Guid.IsEmpty)
                        return "Any";

                    return filter.Guid.ObjectName;
                },

                IsEditable = false,
                Sortable = false,
                MinimumWidth = 100
            };

            OLVColumn GuidCol = new OLVColumn("Guid", "Guid")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is Filter filter))
                        return null;

                    if (filter.Guid.IsEmpty)
                        return "Any";

                    return filter.Guid.GetCounter();
                },

                IsEditable = false,
                Sortable = false,
                MinimumWidth = 100
            };

            OLVColumn EntryCol = new OLVColumn("Entry", "Entry")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is Filter filter))
                        return null;

                    if (!filter.Guid.HasEntry())
                        return "Any";

                    return filter.Guid.GetEntry();
                },

                IsEditable = false,
                Sortable = false,
                MinimumWidth = 80
            };

            OLVColumn TypeCol = new OLVColumn("Type", "Type")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is Filter filter))
                        return null;

                    if (filter.ObjectTypeFilter != ObjectTypeFilter.Any)
                        return filter.ObjectTypeFilter;

                    if (filter.Guid.IsEmpty)
                        return "Any";

                    return filter.Guid.GetObjectType();
                },

                DataType = typeof(ObjectType),
                IsEditable = true,
                Sortable = false,
                FillsFreeSpace = true,
            };

            ObjectFiltersListView.Columns.Add(StateCol);
            ObjectFiltersListView.Columns.Add(NameCol);
            ObjectFiltersListView.Columns.Add(GuidCol);
            ObjectFiltersListView.Columns.Add(EntryCol);
            ObjectFiltersListView.Columns.Add(TypeCol);
        }

        private void EventsListView_FormatRow(object sender, FormatRowEventArgs e)
        {
            if (!(e.Model is SniffedEvent sEvent))
                return;

            switch (SelectedRowColor)
            {
                case RowColorType.None:
                    e.Item.BackColor = Color.White;
                    break;
                case RowColorType.SourceBased:
                    if (sEvent.SourceGuid.IsEmpty)
                        e.Item.BackColor = Color.White;
                    else if (sEvent.SourceGuid.GetObjectType() == ObjectType.GameObject)
                        e.Item.BackColor = Colors.RowColor_GameObject;
                    else if (sEvent.SourceGuid.GetObjectType() == ObjectType.Creature)
                        e.Item.BackColor = Colors.RowColor_Creature;
                    else if (sEvent.SourceGuid.GetObjectType() == ObjectType.Player)
                        e.Item.BackColor = Colors.RowColor_Player;
                    else
                        e.Item.BackColor = Colors.RowColor_Misc;
                    return;
            }
        }

        #region ContextMenuStrip
        private void OnRemoveSource(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sEvent))
            {
                MessageBox.Show("No selected event.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (sEvent.SourceGuid.IsEmpty)
            {
                MessageBox.Show("Event has no source!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Task t = new Task(() =>
            {
                foreach (var toRemoveEvent in eventsListView.Objects.OfType<SniffedEvent>().Where(ev => ev.SourceGuid.Equals(sEvent.SourceGuid)))
                    toRemoveEvent.Discarded = true;
            });
            t.Start();
            t.Wait();

            eventsListView.ClearSelection();
            eventsListView.LightRefreshObjects();
            RefreshFiltering();
        }

        private void OnRemoveTarget(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sEvent))
            {
                MessageBox.Show("No selected event.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (sEvent.TargetGuid.IsEmpty)
            {
                MessageBox.Show("Event has no target!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Task t = new Task(() =>
            {
                foreach (var toRemoveEvent in eventsListView.Objects.OfType<SniffedEvent>().Where(ev => ev.TargetGuid.Equals(sEvent.TargetGuid)))
                    toRemoveEvent.Discarded = true;
            });
            t.Start();
            t.Wait();

            eventsListView.ClearSelection();
            eventsListView.LightRefreshObjects();
            RefreshFiltering();
        }

        private void OnRemoveType(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sEvent))
            {
                MessageBox.Show("No selected event.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Task t = new Task(() =>
            {
                foreach (var toRemoveEvent in eventsListView.Objects.OfType<SniffedEvent>().Where(ev => ev.EventType.Equals(sEvent.EventType)))
                    toRemoveEvent.Discarded = true;
            });
            t.Start();
            t.Wait();

            eventsListView.ClearSelection();
            eventsListView.LightRefreshObjects();
            RefreshFiltering();
        }

        private void OnRemoveRow(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sEvent))
            {
                MessageBox.Show("No selected event.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            sEvent.Discarded = true;
            eventsListView.ClearSelection();
            eventsListView.LightRefreshObjects();
            RefreshFiltering();
        }

        private void OnCopyEventTarget(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sEvent))
            {
                MessageBox.Show("No selected event.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (sEvent.TargetGuid.IsEmpty)
            {
                MessageBox.Show("Event has no target!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetObjectFilterFieldsFromGuid(sEvent.TargetGuid);
        }

        private void OnCopyEventSource(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sEvent))
            {
                MessageBox.Show("No selected event.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (sEvent.SourceGuid.IsEmpty)
            {
                MessageBox.Show("Event has no source!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SetObjectFilterFieldsFromGuid(sEvent.SourceGuid);
        }

        private void OnCopyEventTime(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sEvent))
            {
                MessageBox.Show("No selected event.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            TStripTxtStartTimeValue.Text = sEvent.UnixTime.ToString();
            TStripTxtEndTimeValue.Text = sEvent.UnixTime.ToString();
        }
        #endregion

        #region Async job processing.
        private void ProcessPendingNetworkJobs()
        {
            ThreadPool.QueueUserWorkItem((x) =>
            {
                while (!IsClosing)
                {
                    try
                    {
                        HandleNetworkPacket(PendingNetworkJobs.Take(NetworkJobsCancellationToken.Token));
                    }
                    catch (ArgumentNullException) { Console.WriteLine("BlockingCollection exit."); }
                    catch (OperationCanceledException) { Console.WriteLine("BlockingCollection exit."); } // Exit.
                    catch (Exception ex)
                    {
                        if (IsClosing)
                            return;

                        BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Close();
                        }));
                    }
                }
            });
        }
        #endregion

        #region Network

        private void ConnectToReplayCore()
        {
            if (IsClosing)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action(() => { ConnectToReplayCore(); }));
                return;
            }

            ConnectDlg?.Dispose();
            ConnectDlg = null;
            NetworkClient?.Dispose();
            NetworkClient = null;

            using (ConnectDlg = new FormConnectionDialog())
            {
                ConnectDlg.OnTryConnect += new EventHandler<TryConnectEventArgs>((o, e) =>
                {
                    NetworkClient = new NetworkClient(e.ip, e.port)
                    {
                        OnConnectSuccess = OnConnectSuccess,
                        OnPacketReceived = OnPacketReceived,
                        OnReadError = OnReadError,
                        OnWriteError = OnWriteError
                    };

                    ConnectDlg?.AttachNetworkClient(NetworkClient);
                    NetworkClient?.Connect();
                });


                if (ConnectDlg.ShowDialog(this) != DialogResult.OK)
                    Close();
            }
        }

        private void OnConnectSuccess(object sender, EventArgs e)
        {
            Console.WriteLine("Begin Listening");
        }

        private void OnWriteError(object sender, EventArgs e)
        {
            if (MessageBox.Show((string)sender, "Socket Write Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Retry)
                ConnectToReplayCore();
        }

        private void OnReadError(object sender, EventArgs e)
        {
            if (MessageBox.Show((string)sender, "Socket Read Error", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Retry)
                ConnectToReplayCore();
        }

        private void OnPacketReceived(object sender, EventArgs e)
        {
            if (sender is byte[] bytes)
                PendingNetworkJobs.Add(bytes);
        }


        private void HandleNetworkPacket(byte[] buffer)
        {
            using (ByteBuffer packet = new ByteBuffer(buffer))
            {
                var payloadSize = packet.ReadUInt16(); // Payload Size.
                GUIOpcode opcode = (GUIOpcode)packet.ReadUInt8();
                switch (opcode)
                {
                    case GUIOpcode.SMSG_EVENT_TYPE_LIST:
                        DataHolder.SetEventTypeList(new SMSG_EVENT_TYPE_LIST(packet));
                        ConnectDlg?.BeginDownloadingData();
                        break;
                    case GUIOpcode.SMSG_EVENT_DATA_LIST:
                        foreach (SniffedEvent sEvent in SMSG_EVENT_DATA_LIST.BuildGetEvents(packet))
                            DataHolder.PushSniffedEvent(sEvent);
                        ConnectDlg?.UpdateProgress(DataHolder.GetProgress());
                        break;
                    case GUIOpcode.SMSG_EVENT_DATA_END:
                        SetSniffedEventsModel();
                        break;
                    case GUIOpcode.SMSG_EVENT_DESCRIPTION:
                        var eventDescriptionData = new SMSG_EVENT_DESCRIPTION(packet);
                        BeginInvoke(new Action(() =>
                        {
                            using (eventDescriptionData)
                                if (eventsListView.SelectedObject is SniffedEvent sEvent && sEvent.UUID.Equals(eventDescriptionData.SniffedEventId))
                                    TxtEventDescription.Text += Environment.NewLine + eventDescriptionData.LongDescription;
                        }));
                        break;
                }
            }
        }

        #endregion

        private void SetSniffedEventsModel()
        {
            BeginInvoke(new Action(() =>
            {
                TStripTxtStartTimeValue.Text = DataHolder.GetStartTime().ToString();
                TStripTxtEndTimeValue.Text = DataHolder.GetEndTime().ToString();
                TimeRangeTrackBar.BarMinimum = DataHolder.GetStartTime();
                TimeRangeTrackBar.BarMaximum = DataHolder.GetEndTime();
                TimeRangeTrackBar.RangeMinimum = DataHolder.GetStartTime();
                TimeRangeTrackBar.RangeMaximum = DataHolder.GetEndTime();
                TimeRangeMinFilter = (uint)TimeRangeTrackBar.RangeMinimum;
                TimeRangeMaxFilter = (uint)TimeRangeTrackBar.RangeMaximum;
                TxtTimeRangeMin.Text = DataHolder.GetStartTime().ToString();
                TxtTimeRangeMax.Text = DataHolder.GetEndTime().ToString();
                UpdateTimeRangeValues();
                UpdateEventTypesList();
                DataHolder.UpdateEventTimes();
                eventsListView.Freeze();
                TxtFilter.Text = string.Empty;
                eventsListView.ModelFilter = null;
                eventsListView.ClearObjects();
                eventsListView.SetObjects(DataHolder.SniffedEvents);
                eventsListView.AutoResizeColumns();
                eventsListView.Unfreeze();

                ObjectFiltersListView.SetObjects(DataHolder.GetFilters());
                ObjectFiltersListView.AutoResizeColumns();
                ObjectFiltersListView.SelectedObject = DataHolder.GetFilters().First();

                Text = $"Sniff Browser - Showing {eventsListView.GetItemCount()} Events";
                GC.Collect();
            }));
        }

        private void UpdateEventTypesList()
        {
            if (!DataHolder.HasEvents())
                return;

            EventTypeFilterListView.ModelFilter = new ModelFilter(o =>
            {
                if (!(o is SniffedEventTypeFilterEntry sType))
                    return false;

                if (cmbEventTypes.SelectedIndex == 0)
                    return true; // All.

                if (DataHolder.TryGetEventTypeEntryById((uint)sType.FilterType, out var eventType))
                    if (eventType.EventTypeFilter != (EventTypeFilter)cmbEventTypes.SelectedIndex)
                        return false;

                return true;
            });
        }

        private bool IsVisibleSniffedEvent(SniffedEvent sEvent)
        {
            if (sEvent == null)
                return false;

            if (sEvent.Discarded)
                return false;

            if (TimeRangeMinFilter != 0 && sEvent.UnixTime < TimeRangeMinFilter)
                return false;

            if (TimeRangeMaxFilter != 0 && sEvent.UnixTime > TimeRangeMaxFilter)
                return false;

            if (HasFilters)
                return TickFilters.Any(filter => filter.Evaluate(sEvent));

            return true;
        }

        private void LstEventFilters_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            //if (lstObjectFilters.SelectedItems.Count == 0)
            //    return;

            //// Get the selected item.
            //ListViewItem selectedObject = lstObjectFilters.SelectedItems[0];
            //HashSet<uint> selectedEventTypeList = selectedObject.Tag as HashSet<uint>;

            //ListViewItem item = e.Item;
            //uint eventId = (uint)item.Tag;

            //if (item.Checked)
            //    selectedEventTypeList.Add(eventId);
            //else
            //    selectedEventTypeList.Remove(eventId);

            //if (selectedEventTypeList.Count == DataHolder.GetEventCount())
            //    selectedObject.SubItems[3].Text = "All";
            //else
            //{
            //    string eventTypesList = "";
            //    foreach (var eventType in selectedEventTypeList)
            //    {
            //        if (!String.IsNullOrEmpty(eventTypesList))
            //            eventTypesList += ", ";

            //        eventTypesList += eventType.ToString();
            //    }
            //    selectedObject.SubItems[3].Text = eventTypesList;
            //}

            //EnqueueFiltering();
        }

        private void CmbRowColors_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedRowColor = (RowColorType)cmbRowColors.SelectedIndex;

            switch (SelectedRowColor)
            {
                case RowColorType.None:
                    eventsListView.UseAlternatingBackColors = false;
                    break;
                case RowColorType.Alternating:
                    eventsListView.UseAlternatingBackColors = true;
                    break;
                case RowColorType.SourceBased:
                    eventsListView.UseAlternatingBackColors = false;
                    break;
            }

            eventsListView.LightRefreshObjects();
        }

        private void CmbEventTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateEventTypesList();
        }

        private void UpdateTimeRangeValues()
        {
            TxtTimeRangeMin.Text = TimeRangeTrackBar.RangeMinimum.ToString();
            TxtTimeRangeMax.Text = TimeRangeTrackBar.RangeMaximum.ToString();

            if (uint.TryParse(TxtTimeRangeMin.Text, out uint unixTime))
            {
                DateTime dt = Utility.GetDateTimeFromUnixTime(unixTime);
                LblRangeMinDt.Text = dt.ToString();
            }

            if (uint.TryParse(TxtTimeRangeMax.Text, out unixTime))
            {
                DateTime dt = Utility.GetDateTimeFromUnixTime(unixTime);
                LblRangeMaxDt.Text = dt.ToString();
            }
        }

        private void UpdateStartTime()
        {
            if (uint.TryParse(TStripTxtStartTimeValue.Text, out uint startUnixTime))
            {
                DateTime startTime = Utility.GetDateTimeFromUnixTime(startUnixTime);
                TStripStartTimeLblFormat.Text = startTime.ToString();
            }
        }

        private void UpdateEndTime()
        {
            if (uint.TryParse(TStripTxtEndTimeValue.Text, out uint endUnixTime))
            {
                DateTime endTime = Utility.GetDateTimeFromUnixTime(endUnixTime);
                TStripEndTimeLblFormat.Text = endTime.ToString();
            }
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            if (ObjectFiltersListView.SelectedObject is Filter filter)
            {
                foreach (SniffedEventTypeFilterEntry visibleFilter in EventTypeFilterListView.FilteredObjects)
                    filter.DisableSniffedEventType(visibleFilter.FilterType);
                EventTypeFilterListView.Refresh();
                EnqueueFiltering();
            }
        }

        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            if (ObjectFiltersListView.SelectedObject is Filter filter)
            {
                foreach (SniffedEventTypeFilterEntry visibleFilter in EventTypeFilterListView.FilteredObjects)
                    filter.EnableSniffedEventType(visibleFilter.FilterType);
                EventTypeFilterListView.Refresh();
                EnqueueFiltering();
            }
        }

        private void SetObjectFilterFieldsFromGuid(ObjectGuid guid)
        {
            //int index = (int)guid.GetObjectType().ToString().GetObjectTypeFilterValueFromString();
            //cmbObjectType.SelectedIndex = index;
            //txtObjectGuid.Text = guid.GetCounter().ToString();
            //txtObjectId.Text = guid.GetEntry().ToString();
        }

        private void UpdateTimeDisplayForAllEvents(object sender, EventArgs e)
        {
            SelectedTimeType = cmbTimeType.SelectedIndex;
            SelectedTimeDisplay = cmbTimeDisplay.SelectedIndex;

            if (eventsListView.Objects is ArrayList sniffedEvents && sniffedEvents.Count > 0)
                eventsListView.RefreshObjects(new SniffedEvent[1] { (SniffedEvent)sniffedEvents[0] });
        }

        // Replay Controls

        private void SendChatCommand(string command)
        {
            ByteBuffer packet = new ByteBuffer();
            packet.WriteUInt8((byte)GUIOpcode.CMSG_CHAT_COMMAND);
            packet.WriteCString(command);
            NetworkClient?.SendPacket(packet);
        }

        private void SendRawCommand_Click(object sender, EventArgs e)
        {
            if (!(sender is Control ctrl) || !(ctrl.Tag is string command))
                return;

            SendChatCommand(command);
        }

        private void BtnReplaySkipTime_Click(object sender, EventArgs e)
        {
            string seconds = "10";
            if (Utility.ShowInputDialog(ref seconds, "Seconds to skip") != DialogResult.OK)
                return;

            SendChatCommand("skiptime " + seconds);
        }

        private void BtnReplayJumpToEventTime_Click(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sniffedEvent))
            {
                MessageBox.Show("No event selected.");
                return;
            }

            SendChatCommand("settime " + sniffedEvent.UnixTime.ToString());
        }

        private void SendTeleportToGuid(ObjectGuid guid)
        {
            ByteBuffer packet = new ByteBuffer();
            packet.WriteUInt8((byte)GUIOpcode.CMSG_GOTO_GUID);
            packet.WriteUInt64(guid.RawGuid);
            NetworkClient?.SendPacket(packet);
        }

        private void BtnReplayJumpToEventSource_Click(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sniffedEvent))
            {
                MessageBox.Show("No event selected.");
                return;
            }

            if (sniffedEvent.SourceGuid.IsEmpty)
            {
                MessageBox.Show("Event has no source.");
                return;
            }

            SendTeleportToGuid(sniffedEvent.SourceGuid);
        }

        private void BtnReplayJumpToEventTarget_Click(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sniffedEvent))
            {
                MessageBox.Show("No event selected.");
                return;
            }

            if (sniffedEvent.TargetGuid.IsEmpty)
            {
                MessageBox.Show("Event has no target.");
                return;
            }

            SendTeleportToGuid(sniffedEvent.TargetGuid);
        }

        // Script And Waypoint Making

        private void BtnMakeScript_Click(object sender, EventArgs e)
        {
            HandleMakeScriptOrWaypoints(false);
        }

        private void BtnMakeWaypoints_Click(object sender, EventArgs e)
        {
            HandleMakeScriptOrWaypoints(true);
        }

        private void HandleMakeScriptOrWaypoints(bool makeWaypoints)
        {
            if (!(eventsListView.SelectedObjects is ArrayList sniffedEvents) || sniffedEvents.Count == 0)
            {
                MessageBox.Show("No event selected.");
                return;
            }

            string mainScriptIdStr = "1";
            if (Utility.ShowInputDialog(ref mainScriptIdStr, "Main Script Id") != DialogResult.OK)
                return;
            uint mainScriptId = uint.Parse(mainScriptIdStr);

            string genericScriptIdStr = "50000";
            if (Utility.ShowInputDialog(ref genericScriptIdStr, "Generic Script Id") != DialogResult.OK)
                return;
            uint genericScriptId = uint.Parse(genericScriptIdStr);

            string tableName = "generic_scripts";
            if (Utility.ShowInputDialog(ref tableName, "Table Name") != DialogResult.OK)
                return;

            string commentPrefix = "";
            if (Utility.ShowInputDialog(ref commentPrefix, "Comment Prefix") != DialogResult.OK)
                return;

            bool hasGameObjectSpawn = false;
            List<object> guidList = new List<object>
            {
                ObjectGuid.Empty
            };

            List<uint> Ids = new List<uint>();

            foreach (SniffedEvent sniffedEvent in eventsListView.SelectedObjects.OfType<SniffedEvent>())
            {
                if (!sniffedEvent.SourceGuid.IsEmpty &&
                    !guidList.Contains(sniffedEvent.SourceGuid))
                {
                    guidList.Add(sniffedEvent.SourceGuid);
                }
                if (!sniffedEvent.TargetGuid.IsEmpty &&
                    !guidList.Contains(sniffedEvent.TargetGuid))
                {
                    guidList.Add(sniffedEvent.TargetGuid);
                }
                if (sniffedEvent.SourceGuid.GetObjectType() == ObjectType.GameObject &&
                    sniffedEvent.ShortDescription.Contains("spawns"))
                {
                    hasGameObjectSpawn = true;
                }
                Ids.Add(sniffedEvent.UUID);
            }

            using (var frmListSelector1 = new FormListSelector(guidList, "Make Script", "Select source object:"))
            {
                if (frmListSelector1.ShowDialog() != DialogResult.OK)
                    return;

                ObjectGuid sourceGuid = (ObjectGuid)guidList[frmListSelector1.ReturnValue];

                using (var frmListSelector2 = new FormListSelector(guidList, "Make Script", "Select target object:"))
                    if (frmListSelector2.ShowDialog() != DialogResult.OK)
                        return;

                byte saveGameObjectSpawnsToDatabase = 0;
                if (hasGameObjectSpawn &&
                    MessageBox.Show("Save gameobject spawns to database?", "Make Script", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    saveGameObjectSpawnsToDatabase = 1;

                ObjectGuid targetGuid = (ObjectGuid)guidList[frmListSelector1.ReturnValue];

                ByteBuffer packet = new ByteBuffer();
                packet.WriteUInt8((byte)GUIOpcode.CMSG_MAKE_SCRIPT);
                packet.WriteUInt32(mainScriptId);
                packet.WriteUInt32(genericScriptId);
                packet.WriteCString(tableName);
                packet.WriteCString(commentPrefix);
                packet.WriteUInt64(sourceGuid.RawGuid);
                packet.WriteUInt64(targetGuid.RawGuid);
                packet.WriteUInt8(saveGameObjectSpawnsToDatabase);
                packet.WriteUInt8(makeWaypoints ? (byte)1 : (byte)0);
                // TODO packet.WriteUInt32((uint)lstEvents.SelectedItems.Count);
                foreach (var id in Ids)
                    packet.WriteUInt32(id);

                NetworkClient?.SendPacket(packet);
            }
        }

        private void TxtFilter_TextChanged(object sender, EventArgs e)
        {
            // Prevent from continous filtering while the user types, only trigger if the timer succeeds its interval. 0.7 sec.
            EnqueueFiltering();

            if (string.IsNullOrEmpty(TxtFilter.Text))
                PboxFilterBulb.Visible = false;
            else
                PboxFilterBulb.Visible = true;
        }

        private int PreviousSelection = 0;
        private void EventsListView_SelectionChanged(object sender, EventArgs e)
        {
            if (!(eventsListView.SelectedObject is SniffedEvent sniffedEvent))
            {
                TBtnJumpEventSource.Enabled = false;
                TBtnJumpEventTarget.Enabled = false;
                TBtnJumpEventTime.Enabled = false;

                TxtEventDescription.Text = "";
                return;
            }

            if(PreviousSelection != eventsListView.SelectedIndex)
            {
                Console.WriteLine("Trigger");
                PreviousSelection = eventsListView.SelectedIndex;
                SplitEventsAndDesc.Panel2Collapsed = false;
                SplitEventsAndDesc.Panel1Collapsed = true;
            }

            TBtnJumpEventSource.Enabled = true;
            TBtnJumpEventTarget.Enabled = true;
            TBtnJumpEventTime.Enabled = true;
            TxtEventDescription.Text = sniffedEvent.ToString();
             
            NetworkClient?.RequestEventDescription(sniffedEvent.UUID);
        }

        private void EnqueueFiltering()
        {
            FilteringTimer.Stop();
            FilteringTimer.Start();
        }

        private void FilteringTimer_Tick(object sender, EventArgs e)
        {
            RefreshFiltering();
        }

        private List<Filter> TickFilters;
        private bool HasFilters = false;
        private string FilterText = string.Empty;
        private bool NoTextMatch = true;
        private void RefreshFiltering()
        {
            Cursor = Cursors.WaitCursor;

            if (FilteringTimer.Enabled)
                FilteringTimer.Stop();

            FilterText = !string.IsNullOrEmpty(TxtFilter.Text) ? TxtFilter.Text : string.Empty;
            NoTextMatch = string.IsNullOrEmpty(TxtFilter.Text);

            HasFilters = DataHolder.HasActiveFilters();
            if (HasFilters)
                TickFilters = DataHolder.GetFilters().ToList();

            this.BeginInvoke(new Action(() =>
            {
                eventsListView.ModelFilter = new ModelFilter(o =>
                {
                    if (!(o is SniffedEvent sEvent))
                        return false;

                    if (!IsVisibleSniffedEvent(sEvent))
                        return false;

                    return NoTextMatch || sEvent.ShortDescription.Contains(FilterText, StringComparison.InvariantCultureIgnoreCase);
                });


                eventsListView?.UpdateFilteredEventsTimes();

                Cursor = Cursors.Arrow;
                Text = $"Displaying {eventsListView.GetItemCount()} Events.";
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
            }));
        }

        private void FormSniffBrowser_FormClosing(object sender, FormClosingEventArgs e)
        {
            IsClosing = true;
            try { NetworkClient?.Dispose(); } catch { }
            try { NetworkJobsCancellationToken?.Cancel(false); } catch { }
            try { FilteringTimer?.Dispose(); } catch { }
            try { ImageList?.Dispose(); } catch { }
            Thread.Sleep(1000);
            try { PendingNetworkJobs?.Dispose(); } catch { }
            try { NetworkJobsCancellationToken?.Dispose(); } catch { }
        }

        private void TStripTxtStartTimeValue_TextChanged(object sender, EventArgs e)
        {
            UpdateStartTime();
        }

        private void TStripTxtEndTimeValue_TextChanged(object sender, EventArgs e)
        {
            UpdateEndTime();
        }

        private void TimeRangeTrackBar_ValueChanged(object sender, MouseEventArgs e)
        {
            UpdateTimeRangeValues();
        }

        private void TxtTimeRangeMin_TextChanged(object sender, EventArgs e)
        {
            if (!uint.TryParse(TxtTimeRangeMin.Text, out uint newMinRange))
            {
                if (!PBoxMinRangeError.Visible)
                {
                    PBoxMinRangeError.Visible = true;
                    toolTip1.SetToolTip(PBoxMinRangeError, "Unable to parse to uin32");
                }
                TxtTimeRangeMin.BackColor = Color.LightSalmon;
                return;
            }

            if (newMinRange >= TimeRangeTrackBar.RangeMaximum)
            {
                if (!PBoxMinRangeError.Visible)
                {
                    PBoxMinRangeError.Visible = true;
                    toolTip1.SetToolTip(PBoxMinRangeError, "Min range can't exceed max range.");
                }
                TxtTimeRangeMin.BackColor = Color.LightSalmon;
                return;
            }

            if (newMinRange < TimeRangeTrackBar.BarMinimum)
            {
                if (!PBoxMinRangeError.Visible)
                {
                    PBoxMinRangeError.Visible = true;
                    toolTip1.SetToolTip(PBoxMinRangeError, "Min range can't be smaller than minimum start time.");
                }
                TxtTimeRangeMin.BackColor = Color.LightSalmon;
                return;
            }

            if (PBoxMinRangeError.Visible)
                PBoxMinRangeError.Visible = false;

            TxtTimeRangeMin.BackColor = Color.LightGreen;
            TimeRangeTrackBar.RangeMinimum = newMinRange;
        }

        private void TxtTimeRangeMax_TextChanged(object sender, EventArgs e)
        {
            if (!uint.TryParse(TxtTimeRangeMax.Text, out uint newMaxRange))
            {
                if (!PBoxMaxRangeError.Visible)
                {
                    PBoxMaxRangeError.Visible = true;
                    toolTip1.SetToolTip(PBoxMaxRangeError, "Unable to parse to uin32");
                }
                TxtTimeRangeMax.BackColor = Color.LightSalmon;
                return;
            }

            if (newMaxRange <= TimeRangeTrackBar.RangeMinimum)
            {
                if (!PBoxMaxRangeError.Visible)
                {
                    PBoxMaxRangeError.Visible = true;
                    toolTip1.SetToolTip(PBoxMinRangeError, "Max range can't be smaller than min range.");
                }
                TxtTimeRangeMax.BackColor = Color.LightSalmon;
                return;
            }

            if (newMaxRange > TimeRangeTrackBar.BarMaximum)
            {
                if (!PBoxMaxRangeError.Visible)
                {
                    PBoxMaxRangeError.Visible = true;
                    toolTip1.SetToolTip(PBoxMinRangeError, "Max range can't be greater than maximum end time.");
                }
                TxtTimeRangeMax.BackColor = Color.LightSalmon;
                return;
            }

            if (PBoxMaxRangeError.Visible)
                PBoxMaxRangeError.Visible = false;

            TxtTimeRangeMax.BackColor = Color.LightGreen;
            TimeRangeTrackBar.RangeMaximum = newMaxRange;
        }

        private void BtnApplyTimeRangeFilter_Click(object sender, EventArgs e)
        {
            if (PBoxMaxRangeError.Visible || PBoxMinRangeError.Visible)
            {
                MessageBox.Show("You have range errors.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            TimeRangeMinFilter = (uint)TimeRangeTrackBar.RangeMinimum;
            TimeRangeMaxFilter = (uint)TimeRangeTrackBar.RangeMaximum;
            RefreshFiltering();
        }

        private void ObjectFiltersListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!(ObjectFiltersListView.SelectedObject is Filter filter))
                return;

            using (ObjectSelectionDlg dlg = new ObjectSelectionDlg(filter, TimeRangeMinFilter, TimeRangeMaxFilter))
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                ObjectFiltersListView.RefreshObject(filter);
                TxtFilter.Text = string.Empty;

                if (filter.Enabled)
                    EnqueueFiltering();
            }
        }

        private void TStripObectFilterBtnAdd_Click(object sender, EventArgs e)
        {
            var newFilter = new Filter();
            DataHolder.AddFilter(newFilter);
            ObjectFiltersListView.AddObject(newFilter);
        }

        private void TStripObectFilterBtnRemove_Click(object sender, EventArgs e)
        {
            bool removed = false;
            foreach (var filter in ObjectFiltersListView.SelectedObjects.OfType<Filter>())
            {
                DataHolder.RemoveFilter(filter);
                ObjectFiltersListView.RemoveObject(filter);
                removed = true;
            }

            if (removed)
                EnqueueFiltering();
        }

        private void ObjectFiltersListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ObjectFiltersListView.SelectedObject is Filter filter)
            {
                SplitEventsAndDesc.Panel2Collapsed = true;
                SplitEventsAndDesc.Panel1Collapsed = false;
                EventTypeFilterListView.SetObjects(filter.EventTypeFilter.Values);
            }
            else
            {
                            SplitEventsAndDesc.Panel2Collapsed = false;
            SplitEventsAndDesc.Panel1Collapsed = true;  
                EventTypeFilterListView.ClearObjects();
            }
        }
    }
}
