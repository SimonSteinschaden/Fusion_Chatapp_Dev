using System;
using System.Windows;
using System.Windows.Controls;
using WPF_Fusion.MVVM.ViewModel;

namespace WPF_Fusion.MVVM.View
{
    /// <summary>
    /// Interaction logic for ChatView.xaml
    /// </summary>
    public partial class ChatView : UserControl
    {
        private ChatViewModel? _vm;

        public ChatView()
        {
            InitializeComponent();
            Unloaded += ChatView_Unloaded;
        }

		private async void ChatView_Loaded(object sender, RoutedEventArgs e)
		{
			if (DataContext is not ChatViewModel vm)
				return;

			// The view is re-created whenever the user switches back to the chat tab,
			// so re-subscribing here needs a matching unsubscribe in Unloaded.
			if (!ReferenceEquals(_vm, vm))
			{
				if (_vm != null)
					_vm.MessagesAppended -= OnMessagesAppended;

				_vm = vm;
				_vm.MessagesAppended += OnMessagesAppended;
			}

			await vm.LoadConversationsAsync();
			ScrollToEnd();
		}

		private void ChatView_Unloaded(object sender, RoutedEventArgs e)
		{
			if (_vm != null)
			{
				_vm.MessagesAppended -= OnMessagesAppended;
				_vm = null;
			}
		}

		private void OnMessagesAppended(object? sender, EventArgs e) => ScrollToEnd();

		/// <summary>
		/// Keeps the newest message in view. Without this, incoming messages pile up
		/// below the visible area.
		/// </summary>
		private void ScrollToEnd()
		{
			// Queued at Loaded priority so the new item is laid out before we scroll.
			Dispatcher.BeginInvoke(
				System.Windows.Threading.DispatcherPriority.Loaded,
				new Action(() =>
				{
					if (MessageList.Items.Count == 0)
						return;

					MessageList.ScrollIntoView(MessageList.Items[MessageList.Items.Count - 1]);
				}));
		}
	}
}
