using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ValleyClient.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ValleyClient.ViewModels
{
    public sealed partial class MessageBubble : UserControl
    {
        #region 依赖属性
        // 消息数据源
        public ChatMessage Message
        {
            get => (ChatMessage)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(ChatMessage), typeof(MessageBubble), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MessageBubble bubble && e.NewValue is ChatMessage msg)
            {
                bubble.RefreshUI(msg);
            }
        }
        #endregion

        public MessageBubble()
        {
            InitializeComponent();
        }

        /// <summary>根据消息刷新气泡样式、布局、文字</summary>
        private void RefreshUI(ChatMessage msg)
        {
            bool isSelf = msg.IsSelfSend;
            Border msgBorder = this.FindName("MsgBorder") as Border;

            if (isSelf)
            {
                AvatarBorder.Visibility = Visibility.Collapsed;
                SenderNameTxt.Visibility = Visibility.Collapsed;
                SpacerBorder.Visibility = Visibility.Visible;

                if (msgBorder != null)
                {
                    msgBorder.Style = Resources["SelfBubbleStyle"] as Style;
                    Grid.SetColumn(msgBorder, 2);
                }
            }
            else
            {
                AvatarBorder.Visibility = Visibility.Visible;
                SenderNameTxt.Visibility = Visibility.Visible;
                SpacerBorder.Visibility = Visibility.Collapsed;

                if (msgBorder != null)
                {
                    msgBorder.Style = Resources["OtherBubbleStyle"] as Style;
                    Grid.SetColumn(msgBorder, 1);
                }

                AvatarText.Text = msg.SenderName[0].ToString();
                SenderNameTxt.Text = msg.SenderName;
            }

            MsgContentTxt.Text = msg.Content;
            MsgTimeTxt.Text = msg.SendTime.ToString("HH:mm");
        }
    }
}
