using APIViewModel.MembershipTerms;
using System.Security.Cryptography;
using System.Text;

namespace Services.MembershipTermsService;

public static class MembershipTermsCatalog
{
    public const string CurrentVersion = "membership-terms-v1";
    public static readonly DateOnly EffectiveFrom = new(2026, 11, 12);
    public const string Title = "Điều khoản đăng ký và sử dụng gói tập";

    public static IReadOnlyList<MembershipTermsSectionAPIViewModel> Sections { get; } =
        new List<MembershipTermsSectionAPIViewModel>
        {
            new()
            {
                Id = "PACKAGE_COMMITMENT",
                Title = "1. Cam kết gói tập",
                Body = "Gói tập đã được thanh toán không được hủy. Thời hạn, giá và quyền lợi của gói được giữ nguyên đến hết ngày sử dụng."
            },
            new()
            {
                Id = "PACKAGE_CHANGE",
                Title = "2. Gia hạn, nâng gói và hạ gói",
                Body = "Không nâng hoặc hạ gói giữa kỳ. Gói gia hạn, nâng hoặc hạ được đặt trước chỉ bắt đầu từ ngày liền sau khi gói hiện tại kết thúc và được tính nguyên giá niêm yết tại thời điểm tạo đơn."
            },
            new()
            {
                Id = "PAYMENT",
                Title = "3. Thanh toán",
                Body = "Yêu cầu chưa thanh toán chưa tạo quyền sử dụng và có thể hủy. Gói chỉ được xác nhận sau khi hệ thống ghi nhận thanh toán thành công."
            },
            new()
            {
                Id = "CENTER_RULES",
                Title = "4. Lớp học và an toàn tập luyện",
                Body = "Học viên tuân thủ lịch học, nội quy trung tâm, hướng dẫn an toàn và chỉ dẫn chuyên môn của huấn luyện viên trong quá trình tập luyện."
            }
        };

    public static IReadOnlyList<MembershipTermsAcknowledgementAPIViewModel> RequiredAcknowledgements { get; } =
        new List<MembershipTermsAcknowledgementAPIViewModel>
        {
            new() { Id = "PACKAGE_NON_CANCELABLE", Label = "Tôi hiểu gói đã thanh toán không được hủy." },
            new() { Id = "NO_MID_TERM_CHANGE", Label = "Tôi hiểu không được nâng hoặc hạ gói giữa kỳ." },
            new() { Id = "SCHEDULED_NEXT_PERIOD", Label = "Tôi hiểu gói đặt trước chỉ bắt đầu sau khi gói hiện tại kết thúc." },
            new() { Id = "CENTER_RULES_AND_SAFETY", Label = "Tôi đồng ý tuân thủ nội quy và hướng dẫn an toàn của trung tâm." }
        };

    public static string CurrentHash { get; } = ComputeHash();

    private static string ComputeHash()
    {
        StringBuilder canonical = new();
        canonical.Append(CurrentVersion).Append('\n')
            .Append(EffectiveFrom.ToString("yyyy-MM-dd")).Append('\n')
            .Append(Title).Append('\n');
        foreach (MembershipTermsSectionAPIViewModel section in Sections)
        {
            canonical.Append(section.Id).Append('|')
                .Append(section.Title).Append('|')
                .Append(section.Body).Append('\n');
        }
        foreach (MembershipTermsAcknowledgementAPIViewModel acknowledgement in RequiredAcknowledgements)
        {
            canonical.Append(acknowledgement.Id).Append('|')
                .Append(acknowledgement.Label).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}
