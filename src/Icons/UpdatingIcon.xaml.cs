using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace TaystJkUpdater
{
    public partial class UpdatingIcon : UserControl
    {

        public UpdatingIcon()
        {
            InitializeComponent();

            ((Storyboard)OpenCircle.Resources["Rotate"]).Begin(this, true); // Controllable
            ((Storyboard)OpenCircleClone.Resources["Rotate"]).Begin(this, true); // Controllable
            ((Storyboard)LeftTriangle.Resources["Rotate"]).Begin(this);
            ((Storyboard)RightTriangle.Resources["Rotate"]).Begin(this);
        }

        public async Task MorphIntoCircle()
        {
            ((Storyboard)LeftTriangle.Resources["Collapse"]).Begin(this);
            ((Storyboard)RightTriangle.Resources["Collapse"]).Begin(this);
            ((Storyboard)LetterJ.Resources["Collapse"]).Begin(this);
            ((Storyboard)LetterJSupport.Resources["Collapse"]).Begin(this);
            ((Storyboard)LetterK.Resources["Collapse"]).Begin(this);

            ((Storyboard)OpenCircle.Resources["Rotate"]).Pause(this);
            OpenCircleClone.Visibility = Visibility.Visible;

            await Task.Delay(250);
            ((Storyboard)OpenCircleClone.Resources["Rotate"]).Pause(this);
            await Task.Delay(250); // Wait until the "JK" logo finishes collapsing
        }
    }
}
