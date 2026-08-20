using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Lịch sử hội thoại chatbot (FT-39) — lưu để bệnh nhân xem lại, thay cho quyết định trước đó là không lưu. role phân biệt lượt hỏi (USER) và lượt trả lời (ASSISTANT). Chỉ chủ tài khoản truy cập được lịch sử của mình (§3.2 Restricted).
/// </summary>
public partial class AiChatMessage
{
    public Guid MessageId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public ChatRole Role { get; set; }

    public virtual User User { get; set; } = null!;
}
