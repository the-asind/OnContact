using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

    private static string ConvertBytesToString(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    private static bool EndOfFileReceived(IReadOnlyList<byte> data)
    {
        var endOfFileBytes = Encoding.UTF8.GetBytes("End<>File");
        if (data.Count < endOfFileBytes.Length)
            return false; // The data array is shorter than the "EndOfFile" string, so it can't possibly end with it

        var endOfFileStartIndex = data.Count - endOfFileBytes.Length;
        return !endOfFileBytes.Where((t, i) => data[endOfFileStartIndex + i] != t).Any();
    }


    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ipString = IpTextBox.Text;
            if (!IPAddress.TryParse(ipString, out var ipAddress))
            {
                await ParseServerResponseIntoTextBoxAsync("Invalid IP address.");
                return;
            }

            await _client.ConnectAsync(ipAddress.ToString());
            var response = ConvertBytesToString(await _client.ReceiveMessageAsync(1024));
            if (!string.IsNullOrEmpty(response))
            {
                await ParseServerResponseIntoTextBoxAsync("Server found. " + response);

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
            await ParseServerResponseIntoTextBoxAsync(ex.Message);
        }
    }

    // earlier here was piecewise sequential loading, but due to a number of byte-by-byte troubles this has become problematic.
    [SuppressMessage("ReSharper.DPA", "DPA0001: Memory allocation issues")]
    private async Task HandleServerResponseAsync()
    {
        try
        {
            var answer = Array.Empty<byte>();
            var offsetOfAnswer = 0;
            var sizeOfFile = 0;

            while (true)
            {
                //TODO: добавить хавать со стрима по чанкам
                var response = await _client.ReceiveMessageAsync(1024);
                // append response to answer
                answer = answer.Concat(response).ToArray();
                var answerString = Encoding.UTF8.GetString(answer);

                int FindCountOfDigitInInt(int x)
                {
                    return 1 + (int)Math.Floor(Math.Log(x, 10));
                }

                // Check if we are parsing txt file
                if (answerString[..4] == "!Txt")
                {
                    if (!_isTxtFile)
                    {
                        // parse next numbers into int sizeOfOfile from 6th symbol until space symbol meet
                        sizeOfFile = Convert.ToInt32(answerString.Substring(5, answerString.IndexOf(' ', 5) - 5));
                        //Trace.WriteLine(sizeOfFile);

                        // delete technical symbols: "!Txt(space)<Number>(space)" 
                        answerString = answerString.Remove(0, 6 + FindCountOfDigitInInt(sizeOfFile));
                    }

                    var endIndex = GetEndIndex(answerString);

                    if (IsWholeFileReceived(answer, sizeOfFile))
                    {
                        await ParseEndOfTxtFile(endIndex, answerString, offsetOfAnswer);
                        answer = Array.Empty<byte>();
                        offsetOfAnswer = 0;
                        continue;
                    }

                    await ParseTxtChunkWithoutBrokenChar(endIndex, offsetOfAnswer, answerString);
                    offsetOfAnswer = endIndex;
                }
                // if end of file is received
                else if (EndOfFileReceived(response))
                {
                    answerString = answerString.Remove(answerString.Length - 9, 9); // delete EndOfFile at the end
                    _isTxtFile = false;
                    SendButton.IsEnabled = true;
                    await ParseServerResponseIntoListBoxAsync(answerString);

                    offsetOfAnswer = 0;
                    answer = Array.Empty<byte>();
                    //Trace.WriteLine(answerString);
                }

                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            Client.HandleException(ex);
            await ParseServerResponseIntoTextBoxAsync("!The connection is broken.\n" + ex + "\n");
        }
        finally
        {
            Disconnect();
        }
    }

    private static bool IsWholeFileReceived(byte[] file, int sizeOfFile)
    {
        return file.Length >= sizeOfFile;
    }

    private async Task ParseTxtChunkWithoutBrokenChar(int endIndex, int offsetOfAnswer, string answerString)
    {
        // Parse the server response without the last broken symbol and with the offset
        var length = Math.Max(0, endIndex - offsetOfAnswer);
        await ParseServerResponseIntoTextBoxAsync(answerString.Substring(offsetOfAnswer, length),
            offsetOfAnswer == 0);
        _isTxtFile = true;
    }

    private async Task ParseEndOfTxtFile(int endIndex, string answerString, int offsetOfAnswer)
    {
        await ParseTxtChunkWithoutBrokenChar(endIndex, offsetOfAnswer, answerString);

        SendButton.IsEnabled = true;
        _isTxtFile = false;
    }

    private static int GetEndIndex(string answerString)
    {
        // Check if the last symbol of the answerString is broken in UTF-8
        var endIndex = answerString.Length - 1;

        var answerBytes = Encoding.UTF8.GetBytes(answerString);
        if (answerBytes.Length > 0 && answerBytes[^1] == 0xEF &&
            answerBytes.Length > 2 && answerBytes[^2] == 0xBF &&
            answerBytes[^3] == 0xBD)
            // Set the offsetOfAnswer to exclude the last broken symbol
            endIndex -= 1;
        return endIndex;
    }

    private void EnableAnswerTextBox()
    {
        AnswerListBox.Visibility = Visibility.Collapsed;
        AnswerTextBox.Visibility = Visibility.Visible;
    }

    private void EnableAnswerListBox()
    {
        AnswerTextBox.Visibility = Visibility.Collapsed;
        AnswerListBox.Visibility = Visibility.Visible;
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

    //TODO: баг с передачей текстовых файлов друг за другом. они смещаются криво по оффсету и чего ещё по хуже. Видно EndOfFile
    [SuppressMessage("ReSharper.DPA", "DPA0003: Excessive memory allocations in LOH",
        MessageId = "type: System.String")]
    private Task ParseServerResponseIntoTextBoxAsync(string response, bool isFirstChunkOfFile = false)
    {
        EnableAnswerTextBox();
        //Trace.WriteLine("!!!Response: " + response);
        if (_isTxtFile)
            if (isFirstChunkOfFile)
                AnswerTextBox.Text = response;
            else
                AnswerTextBox.Text += response;
        else
        {
            AnswerTextBox.Text = response;
        }

        return Task.CompletedTask;
    }

    private async Task ParseServerResponseIntoListBoxAsync(string response)
    {
        EnableAnswerListBox();
        //Trace.WriteLine("!!!Response: " + response);
        if (response[0] == '!')
        {
            AnswerListBox.Items.Clear();
            // delete first symbol in response
            response = response.Remove(0, 1);
        }

        var lines = response.Split(Environment.NewLine);

        foreach (var line in lines)
        {
            AnswerListBox.Items.Add(line);
            await Task.Delay(1); // to allow other code to run while waiting
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
        ParseServerResponseIntoTextBoxAsync("Server disconnected.");
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
        var selectedItem = AnswerListBox.SelectedItem?.ToString();

        if (selectedItem is { Length: > 1 } && selectedItem[1] == ':')
        {
            // Get the text content of the selected item and add it to the MessageTextBox
            var selectedText = selectedItem;
            MessageTextBox.Text = selectedText;
        }
    }
}