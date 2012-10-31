// Copyright 2012 Henrik Feldt

using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClientListener
{
	/// <summary>
	/// 	Interaction logic for ShowPic.xaml
	/// </summary>
	public partial class ShowPic : Window
	{
		readonly Uri _imageUri;

		public ShowPic(Uri imageUri)
		{
			_imageUri = imageUri;
			InitializeComponent();
			MainImage.Source = new BitmapImage(_imageUri);
		}

		void button1_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}