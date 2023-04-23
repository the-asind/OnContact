using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using FreeClient;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ClientSideContact;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Client client;

    private async Task HandleServerResponseAsync()
    {
        try
        {
            while (true)
            {
                var response = await client.ReceiveMessageAsync();
                if (!string.IsNullOrEmpty(response))
                {
                    ParseServerResponse(response);
                }
            }
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
            ParseServerResponse("Server unavailable.");
        }
        finally
        {
            Disconnect();
        }
    }

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
                
                //take last five symbols from response into integer
                int port = Convert.ToInt32(response.Substring(response.Length - 3, 3));
                Title = $"On Contact! Client_{port}";
                SendButton.IsEnabled = true;
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                MessageTextBox.IsEnabled = true;
            }
            await HandleServerResponseAsync();
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
            ParseServerResponse(ex.Message.ToString());
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await client.SendMessageAsync(MessageTextBox.Text);
            
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
        }
    }

    private void ParseServerResponse(string response)
    {
        if (response[0] == '!')
        {
            Answer.Items.Clear();
            // delete first symbol in response
            response = response.Remove(0, 1);
        }

        var lines = response.Split(Environment.NewLine);

        foreach (var line in lines) 
            Answer.Items.Add(line);
    }


    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {   
        Disconnect();
    }

    private void Disconnect()
    {
        client.CloseConnection();
        ParseServerResponse("Server disconnected.");
        SendButton.IsEnabled = false;
        ConnectButton.IsEnabled = true;
        DisconnectButton.IsEnabled = false;
    }

    private void Exit_OnClick(object sender, RoutedEventArgs e)
    {
        client.CloseConnection();
        Application.Current.Shutdown();
        Close();
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            MessageTextBox.Text = dialog.FileName;
        }
    }

    private void SelectFile_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            CheckPathExists = true,
            DereferenceLinks = true,
            Filter = "Text Files|*.txt",
            Multiselect = false,
            Title = "Select a File!"
        };
        if (dialog.ShowDialog() == true)
        {
            MessageTextBox.Text = dialog.FileName;
        }
    }

    private void Answer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedItem = Answer.SelectedItem?.ToString();

        if (selectedItem is { Length: > 1 } && selectedItem[1] == ':')
        {
            // Get the text content of the selected item and add it to the MessageTextBox
            string selectedText = selectedItem;
            MessageTextBox.Text = selectedText;
        }
    }

}