using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

    private static string ConvertBytesToString(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    private static bool EndOfFileReceived(IReadOnlyList<byte> data)
    {
        var endOfFileBytes = Encoding.UTF8.GetBytes("EndOfFile");
        if (data.Count < endOfFileBytes.Length)
            return false; // The data array is shorter than the "EndOfFile" string, so it can't possibly end with it

        var endOfFileStartIndex = data.Count - endOfFileBytes.Length;
        return !endOfFileBytes.Where((t, i) => data[endOfFileStartIndex + i] != t).Any();
    }


    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _client.ConnectAsync(IPAddress.Loopback.ToString());
            var response = ConvertBytesToString(await _client.ReceiveMessageAsync());
            if (!string.IsNullOrEmpty(response))
            {
                await ParseServerResponseAsync("Server found. " + response);

                //take last five symbols from response into integer
                var port = Convert.ToInt32(response.Substring(response.Length - 3, 3));
                Title = $"On Contact! Client_{port}\n";
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

    // earlier here was piecewise sequential loading, but due to a number of byte-by-byte troubles this has become problematic.
    private async Task HandleServerResponseAsync()
    {
        try
        {
            var answer = new StringBuilder();
            var offsetOfAnswer = 0;
            while (true)
            {
                var response = await _client.ReceiveMessageAsync();
                answer.Append(ConvertBytesToString(response));
                var answerString = answer.ToString();
                // if end of file is received
                if (EndOfFileReceived(response))
                {
                    answerString = answerString.Remove(answerString.Length - 9, 9); // delete EndOfFile at the end

                    if (answerString[..4] == "!Txt")
                    {
                        answerString = answerString.Remove(0, 4);
                        _isTxtFile = true;
                        await ParseServerResponseAsync(answerString, true);
                    }
                    else
                    {
                        _isTxtFile = false;
                        await ParseServerResponseAsync(answerString);
                    }

                    SendButton.IsEnabled = true;

                    offsetOfAnswer = 0;
                    answer.Clear();
                    Trace.WriteLine(answerString);
                }
                // Check if we parsing txt file
                else if (answerString[..4] == "!Txt")
                {
                    // Check if the last symbol of the answerString is broken in UTF-8
                    var endIndex = answerString.Length - 1;

                    var answerBytes = Encoding.UTF8.GetBytes(answerString);
                    if (answerBytes.Length > 0 && answerBytes[^1] == 0xEF &&
                        answerBytes.Length > 2 && answerBytes[^2] == 0xBF &&
                        answerBytes[^3] == 0xBD)
                        // Set the offsetOfAnswer to exclude the last broken symbol
                        endIndex -= 1;

                    // Parse the server response without the last broken symbol and with the offset
                    await ParseServerResponseAsync(answerString.Substring(offsetOfAnswer, endIndex),
                        offsetOfAnswer == 0);
                    offsetOfAnswer = endIndex;
                }
            }
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
            await ParseServerResponseAsync("!The connection is broken.\n" + ex.Message + "\n");
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

    private Task ParseServerResponseAsync(string response, bool isFirstChunkOfFile = false)
    {
        Trace.WriteLine("!!!Response: " + response);
        if (_isTxtFile)
            if (isFirstChunkOfFile)
                Answer.Text = response;
            else
                Answer.Text += response;
        else
        {
            if (response[0] == '!')
            {
                Answer.Text = "";
                // delete first symbol in response
                response = response.Remove(0, 1);
            }

            Answer.Text += response;
        }

        return Task.CompletedTask;
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

    // private void Answer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    // {
    //     var selectedItem = Answer.SelectedItem?.ToString();
    //
    //     if (selectedItem is { Length: > 1 } && selectedItem[1] == ':')
    //     {
    //         // Get the text content of the selected item and add it to the MessageTextBox
    //         var selectedText = selectedItem;
    //         MessageTextBox.Text = selectedText;
    //     }
    // }
}