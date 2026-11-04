using System.Windows;
using System.Windows.Input;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Host.Wpf;

public partial class LetterDetailWindow : Window
{
    private readonly LetterDef _letter;
    private readonly ISocialNetwork _social;
    private readonly Action? _onStateChanged;
    private bool _isClaimed;

    public LetterDetailWindow(
        LetterDef letter,
        string senderCitadelName,
        ISocialNetwork social,
        Action? onStateChanged = null)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _letter = letter;
        _social = social;
        _onStateChanged = onStateChanged;
        _isClaimed = letter.IsClaimed;

        Title = $"Thư Tín — {letter.TitleVi}";
        TitleText.Text = letter.TitleVi;
        SenderText.Text = $"Người gửi: {letter.SenderNameVi} (Hiện ở {senderCitadelName})";
        DateText.Text = $"Ngày gửi: {GameDateCodec.Format(letter.SentDate)}";
        ContentText.Text = letter.ContentVi;

        // Automatically mark as read if not already
        if (!letter.IsRead)
        {
            _social.ReadLetter(letter.Id);
            _onStateChanged?.Invoke();
        }

        UpdateGiftSection();

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private void UpdateGiftSection()
    {
        var hasGift = _letter.GiftCopper > 0 || _letter.GiftItemQuantity > 0;
        if (!hasGift)
        {
            GiftBorder.Visibility = Visibility.Collapsed;
            return;
        }

        GiftBorder.Visibility = Visibility.Visible;

        var giftParts = new List<string>();
        if (_letter.GiftCopper > 0)
        {
            giftParts.Add($"{_letter.GiftCopper} Quan tiền");
        }
        if (_letter.GiftItemQuantity > 0)
        {
            var itemName = _letter.GiftItemNameVi ?? "vật phẩm";
            giftParts.Add($"{_letter.GiftItemQuantity} {itemName}");
        }

        var giftStr = string.Join(" và ", giftParts);

        if (_isClaimed)
        {
            GiftSummaryText.Text = $"✅ {giftStr} (Đã nhận vào hành trang)";
            ClaimGiftBtn.IsEnabled = false;
            ClaimGiftBtn.Content = "Đã Nhận";
        }
        else
        {
            GiftSummaryText.Text = $"🎁 {giftStr}";
            ClaimGiftBtn.IsEnabled = true;
            ClaimGiftBtn.Content = "Nhận Quà 🎁";
        }
    }

    private void ClaimGift_Click(object sender, RoutedEventArgs e)
    {
        if (_isClaimed) return;

        var msg = _social.ClaimLetterGift(_letter.Id);
        _isClaimed = true;
        UpdateGiftSection();
        StatusNoticeText.Text = msg;
        _onStateChanged?.Invoke();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
