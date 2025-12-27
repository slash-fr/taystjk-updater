using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Updater
{
    public partial class ErrorIcon : UserControl
    {
        public ErrorIcon()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Play an animation to show the full icon
        /// </summary>
        public async Task AnimateIn()
        {
            ((Storyboard)Bar.Resources["AnimateIn"]).Begin(this);
            ((Storyboard)Dot.Resources["AnimateIn"]).Begin(this);
            await Task.Delay(500);
        }

        /// <summary>
        /// Play an animation to hide the exclamation mark
        /// </summary>
        public async Task AnimateOut()
        {
            ((Storyboard)Bar.Resources["AnimateOut"]).Begin(this);
            ((Storyboard)Dot.Resources["AnimateOut"]).Begin(this);
            await Task.Delay(500);
        }
    }
}
