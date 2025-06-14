using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Text;

namespace OneCalculator;

public partial class MainPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _expressionDisplay = string.Empty;

    [ObservableProperty]
    private string _resultDisplay = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CalculationHistory> _historyItems = new();

    [ObservableProperty]
    private CalculationHistory _selectedHistoryItem;

    private bool _lastInputWasOperator;
    private bool _parenthesesOpen;

    [RelayCommand]
    private async Task ShowHistory()
    {
        if (HistoryItems.Count == 0)
        {
            await Shell.Current.DisplayAlert("History", "No history available yet", "OK");
            return;
        }

        // Create buttons for each history item
        var buttons = HistoryItems
            .Select(h => $"{FormatNumberWithCommas(h.Expression)} = {FormatNumberWithCommas(h.Result)}")
            .ToArray();

        var result = await Shell.Current.DisplayActionSheet(
            "Calculation History",
            "Cancel",
            "Clear History",
            buttons);

        if (result == "Clear History")
        {
            HistoryItems.Clear();
        }
        else if (result != "Cancel" && buttons.Contains(result))
        {
            // Find the matching history item
            var selectedHistory = HistoryItems.FirstOrDefault(h =>
                $"{FormatNumberWithCommas(h.Expression)} = {FormatNumberWithCommas(h.Result)}" == result);

            if (selectedHistory != null)
            {
                ExpressionDisplay = selectedHistory.Expression;
                ResultDisplay = selectedHistory.Result;
            }
        }
    }

    [RelayCommand]
    private async Task ChooseTheme()
    {
        var currentTheme = Application.Current?.UserAppTheme ?? AppTheme.Unspecified;
        var currentThemeName = currentTheme switch
        {
            AppTheme.Light => "Light",
            AppTheme.Dark => "Dark",
            _ => "System Default"
        };

        var result = await Shell.Current.DisplayActionSheet("Choose Theme", "Cancel", null,
            $"{(currentThemeName == "Light" ? "✓ " : "")}Light",
            $"{(currentThemeName == "Dark" ? "✓ " : "")}Dark",
            $"{(currentThemeName == "System Default" ? "✓ " : "")}System Default");

        if (result == "Cancel") return;

        // Remove the checkmark if present
        var selectedTheme = result.Replace("✓ ", "");

        var theme = selectedTheme switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            "System Default" => AppTheme.Unspecified,
            _ => AppTheme.Unspecified
        };

        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = theme;
            Preferences.Set("SelectedTheme", selectedTheme);
        }
    }

    [RelayCommand]
    private async Task ShowPrivacyPolicy()
    {
        await Shell.Current.DisplayAlert("Privacy Policy",
            "This app respects your privacy. We don't collect any personal data.", "OK");
    }

    [RelayCommand]
    private async Task SendFeedback()
    {
        var email = new EmailMessage
        {
            Subject = "Feedback for OneCalculator",
            Body = "I have some feedback about the calculator app:",
            To = new List<string> { "feedback@example.com" }
        };

        try
        {
            await Email.ComposeAsync(email);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not send email: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task ShowHelp()
    {
        await Shell.Current.DisplayAlert("Help",
            "Calculator Help:\n\n" +
            "- Use number buttons to input values\n" +
            "- Operators: +, -, ×, ÷, %\n" +
            "- AC: Clear all\n" +
            "- DE: Delete last character\n" +
            "- ( ): Toggle parentheses\n" +
            "- =: Calculate result", "OK");
    }

    [RelayCommand]
    public void HandleButtonPress(string buttonText)
    {
        switch (buttonText)
        {
            case "AC":
                ExpressionDisplay = string.Empty;
                ResultDisplay = string.Empty;
                _lastInputWasOperator = false;
                _parenthesesOpen = false;
                break;

            case "DE":
                if (ExpressionDisplay.Length > 0)
                {
                    var lastChar = ExpressionDisplay[^1];
                    if (lastChar == '(') _parenthesesOpen = false;
                    else if (lastChar == ')') _parenthesesOpen = true;

                    ExpressionDisplay = ExpressionDisplay[..^1];
                    _lastInputWasOperator = IsOperator(lastChar.ToString());
                }
                break;

            case "( )":
                if (!_parenthesesOpen)
                {
                    ExpressionDisplay += "(";
                    _parenthesesOpen = true;
                }
                else
                {
                    ExpressionDisplay += ")";
                    _parenthesesOpen = false;
                }
                _lastInputWasOperator = false;
                break;

            case "=":
                if (string.IsNullOrEmpty(ExpressionDisplay)) return;

                try
                {
                    // Replace the multiplication and division symbols with ones that DataTable.Compute understands
                    var expression = ExpressionDisplay
                        .Replace("×", "*")
                        .Replace("÷", "/")
                        .Replace("−", "-");

                    var result = new DataTable().Compute(expression, null);
                    if (result != null)
                    {
                        var formattedResult = FormatNumberWithCommas(result.ToString());
                        ResultDisplay = $"= {formattedResult}";
                    }
                    else
                    {
                        ResultDisplay = "= Error";
                    }

                    // Add to history (limit to 20 items)
                    HistoryItems.Insert(0, new CalculationHistory
                    {
                        Expression = ExpressionDisplay,
                        Result = ResultDisplay.Replace("= ", "")
                    });

                    if (HistoryItems.Count > 20)
                    {
                        HistoryItems.RemoveAt(HistoryItems.Count - 1);
                    }
                }
                catch
                {
                    ResultDisplay = "= Error";
                }
                break;

            case "+":
            case "−":
            case "×":
            case "÷":
            case "%":
                if (ExpressionDisplay.Length == 0) return;

                // Replace the last operator if the last input was an operator
                if (_lastInputWasOperator)
                {
                    ExpressionDisplay = ExpressionDisplay[..^1] + buttonText;
                }
                else
                {
                    ExpressionDisplay += buttonText;
                }
                _lastInputWasOperator = true;
                break;

            case ".":
                if (string.IsNullOrEmpty(ExpressionDisplay))
                {
                    ExpressionDisplay += "0.";
                }
                else if (_lastInputWasOperator)
                {
                    ExpressionDisplay += "0.";
                }
                else
                {
                    // Check if the current number already has a decimal point
                    var lastNumber = GetLastNumber(ExpressionDisplay);
                    if (!lastNumber.Contains('.'))
                    {
                        ExpressionDisplay += ".";
                    }
                }
                _lastInputWasOperator = false;
                break;

            default: // Numbers and other characters
                ExpressionDisplay += buttonText;
                _lastInputWasOperator = false;
                break;
        }
    }

    private bool IsOperator(string input)
    {
        return input == "+" || input == "−" || input == "×" || input == "÷" || input == "%";
    }

    private string GetLastNumber(string expression)
    {
        if (string.IsNullOrEmpty(expression)) return string.Empty;

        var lastNumber = string.Empty;
        for (int i = expression.Length - 1; i >= 0; i--)
        {
            var c = expression[i];
            if (char.IsDigit(c) || c == '.')
            {
                lastNumber = c + lastNumber;
            }
            else if (IsOperator(c.ToString()) || c == '(' || c == ')')
            {
                break;
            }
        }
        return lastNumber;
    }

    private string FormatNumberWithCommas(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Split the input into tokens (numbers and operators)
        var tokens = new List<string>();
        var currentToken = new StringBuilder();

        foreach (char c in input)
        {
            if (char.IsDigit(c) || c == '.')
            {
                currentToken.Append(c);
            }
            else
            {
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
                tokens.Add(c.ToString());
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        // Format each number token
        for (int i = 0; i < tokens.Count; i++)
        {
            if (double.TryParse(tokens[i], out double number))
            {
                // Check if it's a whole number
                if (tokens[i].Contains('.'))
                {
                    // For decimal numbers, format the whole number part
                    var parts = tokens[i].Split('.');
                    if (long.TryParse(parts[0], out long wholeNumber))
                    {
                        parts[0] = wholeNumber.ToString("N0", CultureInfo.InvariantCulture);
                        tokens[i] = string.Join(".", parts);
                    }
                }
                else
                {
                    // For whole numbers
                    if (long.TryParse(tokens[i], out long wholeNumber))
                    {
                        tokens[i] = wholeNumber.ToString("N0", CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        return string.Join("", tokens);
    }
}

public class CalculationHistory
{
    public string Expression { get; set; }
    public string Result { get; set; }
}