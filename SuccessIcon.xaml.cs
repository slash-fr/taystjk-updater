using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Updater
{
    public partial class SuccessIcon : UserControl
    {
        public SuccessIcon()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Play an animation to show the full icon
        /// </summary>
        public async Task AnimateIn()
        {
            FullSymbol.Visibility = Visibility.Visible;
            ((Storyboard)FullSymbol.Resources["AnimateIn"]).Begin(this);
            await Task.Delay(1000);
        }
    }
}
