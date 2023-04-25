using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FreeClient;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ClientSideContact;

public partial class MainWindow
{
    private readonly Client _client;
    private bool _isTxtFile;

    public MainWindow()
    {
        InitializeComponent();
        _client = new Client(new TcpClient());
        SendButton.IsEnabled = false;
        ConnectButton.IsEnabled = true;
        DisconnectButton.IsEnabled = false;
        MessageTextBox.IsEnabled = false;
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _client.ConnectAsync(IPAddress.Loopback.ToString());
            var response = await _client.ReceiveMessageAsync();
            if (!string.IsNullOrEmpty(response))
            {
                await ParseServerResponseAsync("Server found. " + response);

                //take last five symbols from response into integer
                var port = Convert.ToInt32(response.Substring(response.Length - 3, 3));
                Title = $"On Contact! Client_{port}";
                SendButton.IsEnabled = true;
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                MessageTextBox.IsEnabled = true;
            }

            IpTextBox.IsReadOnly = true;
            await HandleServerResponseAsync();
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
            await ParseServerResponseAsync(ex.Message);
        }
    }

    private async Task HandleServerResponseAsync()
    {
        try
        {
            while (true)
            {
                var response = await _client.ReceiveMessageAsync();
                if (!string.IsNullOrEmpty(response))
                    if (response[..4] == "!Txt")
                    {
                        response = response.Remove(0, 4);
                        _isTxtFile = true;
                        await ParseServerResponseAsync(response, true);
                    }

                if (response.Substring(response.Length - 9, 9) == "EndOfFile")
                {
                    response = response.Remove(response.Length - 9, 9);
                    _isTxtFile = false;
                    await ParseServerResponseAsync(response);
                    SendButton.IsEnabled = true;
                }
                else
                {
                    await ParseServerResponseAsync(response);
                }

                await Task.Delay(1); // Wait for 100ms before checking for data again
            }
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
            await ParseServerResponseAsync("!The connection is broken.");
        }
        finally
        {
            Disconnect();
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _client.SendMessageAsync(MessageTextBox.Text);
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
        }
        finally
        {
            SendButton.IsEnabled = false;
        }
    }

    private async Task ParseServerResponseAsync(string response, bool isFirstChunkOfFile = false)
    {
        Trace.WriteLine("!!!Response: " + response);
        if (_isTxtFile)
        {
            //add into first item in Answer listbox if it is not empty
            if (Answer.Items.Count > 1)
                Answer.Items.Clear();

            if (Answer.Items.Count > 0)
                if (isFirstChunkOfFile)
                    Answer.Items[0] = response;
                else
                    Answer.Items[0] += response;
            else Answer.Items.Add(response);
        }
        else //TODO: fix problem with listbox overloading (extra lines in txt format) maybe change into just textbox
        {
            if (response[0] == '!')
            {
                Answer.Items.Clear();
                // delete first symbol in response
                response = response.Remove(0, 1);
            }

            var lines = response.Split(Environment.NewLine);

            foreach (var line in lines)
            {
                Answer.Items.Add(line);
                await Task.Delay(1); // to allow other code to run while waiting
            }
        }
    }


    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        Disconnect();
    }

    private void Disconnect()
    {
        Title = "On Contact!";
        _client.CloseConnection();
        ParseServerResponseAsync("Server disconnected.");
        SendButton.IsEnabled = false;
        ConnectButton.IsEnabled = true;
        DisconnectButton.IsEnabled = false;
        IpTextBox.IsReadOnly = false;
    }

    private void Exit_OnClick(object sender, RoutedEventArgs e)
    {
        _client.CloseConnection();
        Application.Current.Shutdown();
        Close();
    }

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog();
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok) MessageTextBox.Text = dialog.FileName;
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
        if (dialog.ShowDialog() == true) MessageTextBox.Text = dialog.FileName;
    }

    private void Answer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedItem = Answer.SelectedItem?.ToString();

        if (selectedItem is { Length: > 1 } && selectedItem[1] == ':')
        {
            // Get the text content of the selected item and add it to the MessageTextBox
            var selectedText = selectedItem;
            MessageTextBox.Text = selectedText;
        }
    }
}