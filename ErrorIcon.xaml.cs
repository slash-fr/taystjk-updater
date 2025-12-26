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
        public void AnimateIn()
        {
            ((Storyboard)Bar.Resources["AnimateIn"]).Begin(this);
            ((Storyboard)Dot.Resources["AnimateIn"]).Begin(this);
        }
    }
}
