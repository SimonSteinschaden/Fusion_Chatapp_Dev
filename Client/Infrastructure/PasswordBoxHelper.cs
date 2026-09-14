using System.Windows;
using System.Windows.Controls;

namespace WPF_Fusion.Infrastructure
{
	/// <summary>
	/// Enables two-way data binding for <see cref="PasswordBox.Password"/>,
	/// which is not bindable by default in WPF.
	/// </summary>
	public static class PasswordBoxHelper
	{
		/// <summary>
		/// Attached property that stores the bound password value.
		/// </summary>
		public static readonly DependencyProperty BoundPasswordProperty =
			DependencyProperty.RegisterAttached(
				"BoundPassword",
				typeof(string),
				typeof(PasswordBoxHelper),
				new FrameworkPropertyMetadata(
					string.Empty,
					FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
					OnBoundPasswordChanged));

		/// <summary>
		/// Attached property that enables or disables password binding behavior.
		/// When set to true, the helper synchronizes the PasswordBox with the bound value.
		/// </summary>
		public static readonly DependencyProperty BindPasswordProperty =
			DependencyProperty.RegisterAttached(
				"BindPassword",
				typeof(bool),
				typeof(PasswordBoxHelper),
				new PropertyMetadata(false, OnBindPasswordChanged));

		/// <summary>
		/// Internal flag used to prevent recursive updates between the
		/// PasswordBox and the bound password property.
		/// </summary>
		private static readonly DependencyProperty IsUpdatingProperty =
			DependencyProperty.RegisterAttached(
				"IsUpdating",
				typeof(bool),
				typeof(PasswordBoxHelper),
				new PropertyMetadata(false));

		/// <summary>
		/// Sets the bound password value.
		/// </summary>
		public static void SetBoundPassword(DependencyObject dp, string value) =>
			dp.SetValue(BoundPasswordProperty, value);

		/// <summary>
		/// Gets the bound password value.
		/// </summary>
		public static string GetBoundPassword(DependencyObject dp) =>
			(string)dp.GetValue(BoundPasswordProperty);

		/// <summary>
		/// Enables or disables the password binding behavior.
		/// </summary>
		public static void SetBindPassword(DependencyObject dp, bool value) =>
			dp.SetValue(BindPasswordProperty, value);

		/// <summary>
		/// Gets whether password binding behavior is enabled.
		/// </summary>
		public static bool GetBindPassword(DependencyObject dp) =>
			(bool)dp.GetValue(BindPasswordProperty);

		private static void SetIsUpdating(DependencyObject dp, bool value) =>
			dp.SetValue(IsUpdatingProperty, value);

		private static bool GetIsUpdating(DependencyObject dp) =>
			(bool)dp.GetValue(IsUpdatingProperty);

		/// <summary>
		/// Attaches or detaches the PasswordChanged event handler
		/// based on the BindPassword property value.
		/// </summary>
		private static void OnBindPasswordChanged(
			DependencyObject dp,
			DependencyPropertyChangedEventArgs e)
		{
			if (dp is not PasswordBox passwordBox)
				return;

			if ((bool)e.OldValue)
				passwordBox.PasswordChanged -= HandlePasswordChanged;

			if ((bool)e.NewValue)
				passwordBox.PasswordChanged += HandlePasswordChanged;
		}

		/// <summary>
		/// Updates the PasswordBox.Password value when the bound password changes.
		/// </summary>
		private static void OnBoundPasswordChanged(
			DependencyObject dp,
			DependencyPropertyChangedEventArgs e)
		{
			if (dp is not PasswordBox passwordBox)
				return;

			passwordBox.PasswordChanged -= HandlePasswordChanged;

			if (!GetIsUpdating(passwordBox))
				passwordBox.Password = e.NewValue?.ToString() ?? string.Empty;

			passwordBox.PasswordChanged += HandlePasswordChanged;
		}

		/// <summary>
		/// Handles the PasswordChanged event and updates the bound password value.
		/// </summary>
		private static void HandlePasswordChanged(
			object sender,
			RoutedEventArgs e)
		{
			if (sender is not PasswordBox passwordBox)
				return;

			SetIsUpdating(passwordBox, true);
			SetBoundPassword(passwordBox, passwordBox.Password);
			SetIsUpdating(passwordBox, false);
		}
	}
}
