using System;
using System.Net;
using System.Windows;
using FreeClient;

namespace ClientSideContact;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Client client;

    public MainWindow()
    {
        InitializeComponent();
        client = new FreeClient.Client();
        SendButton.IsEnabled = false;
        ConnectButton.IsEnabled = true;
        DisconnectButton.IsEnabled = false;
        MessageTextBox.IsEnabled = false;
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await client.ConnectAsync(IPAddress.Loopback.ToString());
            var response = await client.ReceiveMessageAsync();
            if (!string.IsNullOrEmpty(response)) 
            {
                ParseServerResponse("Server found. " + response);
                SendButton.IsEnabled = true;
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                MessageTextBox.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await client.SendMessageAsync(MessageTextBox.Text);
            var response = await client.ReceiveMessageAsync();
            if (!string.IsNullOrEmpty(response)) 
            {
                ParseServerResponse(response);
            }
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
        }
    }
    
    private void ParseServerResponse(string response)
    {
        AnswerTextBox.Text = response;
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        client.CloseConnection();
        ParseServerResponse("Server disconnected.");
        SendButton.IsEnabled = false;
        ConnectButton.IsEnabled = true;
        DisconnectButton.IsEnabled = false;
    }
}