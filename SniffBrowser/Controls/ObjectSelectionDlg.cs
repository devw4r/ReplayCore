using System;
using System.Windows.Forms;
using System.Collections.Generic;

using BrightIdeasSoftware;
using SniffBrowser.Core;
using System.Linq;

namespace SniffBrowser.Controls
{
    public partial class ObjectSelectionDlg : Form
    {
        private readonly uint RangeMin;
        private readonly uint RangeMax;
        private readonly Filter Filter;
        public ObjectSelectionDlg(Filter filter, uint rangeMin, uint rangeMax)
        {
            RangeMax = rangeMax;
            RangeMin = rangeMin;
            Filter = filter;
            InitializeComponent();

            ChkBoxByObjectType.Checked = filter.ObjectTypeFilter != ObjectTypeFilter.Any;

            OLVColumn ObjectNameCol = new OLVColumn("Name", "Name")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is ObjectGuid oGuid) || string.IsNullOrEmpty(oGuid.ObjectName))
                        return "n/a";

                    return oGuid.ObjectName;
                },

                UseFiltering = true,
                MinimumWidth = 80,
            };

            OLVColumn TypeCol = new OLVColumn("Type", "Type")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is ObjectGuid oGuid))
                        return null;

                    return oGuid.GetObjectType();
                },

                UseFiltering = true,
                MinimumWidth = 100,
            };


            OLVColumn HighTypeCol = new OLVColumn("HighType", "HighType")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is ObjectGuid oGuid))
                        return null;

                    return oGuid.GetHighType();
                },

                UseFiltering = true,
                MinimumWidth = 100,
            };

            OLVColumn GuidCol = new OLVColumn("Guid", "Guid")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is ObjectGuid oGuid))
                        return null;

                    return oGuid.GetCounter();
                },

                MinimumWidth = 120,
                UseFiltering = true,
            };

            OLVColumn EntryCol = new OLVColumn("Entry", "Entry")
            {
                AspectGetter = delegate (object o)
                {
                    if (!(o is ObjectGuid oGuid))
                        return 0;

                    if (!oGuid.HasEntry())
                        return 0;

                    return oGuid.GetEntry();
                },

                UseFiltering = true,
            };

            availableObjectsListView.FullRowSelect = true;
            availableObjectsListView.UseFiltering = true;          
            availableObjectsListView.Columns.Add(ObjectNameCol);
            availableObjectsListView.Columns.Add(TypeCol);
            availableObjectsListView.Columns.Add(HighTypeCol);
            availableObjectsListView.Columns.Add(GuidCol);
            availableObjectsListView.Columns.Add(EntryCol);
            availableObjectsListView.SetObjects(DataHolder.ObjectGuidMap.Values);
            availableObjectsListView.AutoResizeColumns();

            CBoxObjectTypes.DataSource = EnumUtils<ObjectTypeFilter>.Values;            

            if (ChkBoxByObjectType.Checked)
            {
                PnlList.Enabled = false;
                CBoxObjectTypes.Enabled = true;
                CBoxObjectTypes.SelectedIndex = (int)filter.ObjectTypeFilter;
            }
            else
            {
                CBoxObjectTypes.Enabled = false;
                RefreshFiltering();
            }
        }

        private void RefreshFiltering()
        {
            TextMatchFilter filter = null;
            if (!string.IsNullOrEmpty(TxtFilter.Text))
            {
                filter = TextMatchFilter.Contains(availableObjectsListView, TxtFilter.Text);
                availableObjectsListView.ModelFilter = filter;
                availableObjectsListView.DefaultRenderer = new HighlightTextRenderer(filter);
            }

            var tfilter = new ModelFilter(o =>
            {
                if (!(o is ObjectGuid oGuid))
                    return false;

                // If any of the events linked to this object are within the current time frame, display.
                foreach(var sEvent in oGuid.SniffedEvents)
                    if (sEvent.UnixTime >= RangeMin && sEvent.UnixTime <= RangeMax)
                        return true;

                return false;
            });

            if (filter == null)
                availableObjectsListView.ModelFilter = tfilter;
            else
                availableObjectsListView.ModelFilter = new CompositeAllFilter(new List<IModelFilter> { filter, tfilter });
        }

        private void TxtFilter_TextChanged(object sender, EventArgs e)
        {
            RefreshFiltering();
        }

        private void ChkBoxByObjectType_CheckedChanged(object sender, EventArgs e)
        {
            CBoxObjectTypes.Enabled = ChkBoxByObjectType.Checked;
            PnlList.Enabled = !ChkBoxByObjectType.Checked;
        }

        private void AvailableObjectsListView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (!PnlList.Enabled)
                return;

            if (availableObjectsListView.SelectedObject is ObjectGuid oGuid)
            {
                Filter.Guid = oGuid;
                Filter.ObjectTypeFilter = ObjectTypeFilter.Any;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (ChkBoxByObjectType.Checked)
            {
                Filter.Guid = ObjectGuid.Empty;
                Filter.ObjectTypeFilter = (ObjectTypeFilter)CBoxObjectTypes.SelectedValue;
            }
            else
            {
                if (availableObjectsListView.SelectedObject is ObjectGuid oGuid)
                {
                    Filter.Guid = oGuid;
                    Filter.ObjectTypeFilter = ObjectTypeFilter.Any;
                }
                else
                {
                    MessageBox.Show("Missing object selection.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
