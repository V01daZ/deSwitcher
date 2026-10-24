using System.Windows.Controls;
using DeSwitcher.Patcher.Logic;

namespace DeSwitcher.Patcher.Views
{
    public partial class ScanningView : UserControl
    {
        public ScanningView()
        {
            InitializeComponent();
            Title.Text = Loc.T("scan.running");
            Count.Text = "";
        }

        public void SetProgress(int done, int total)
        {
            Count.Text = total > 0 ? done + " / " + total : "";
        }
    }
}
