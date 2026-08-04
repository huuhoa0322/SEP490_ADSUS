namespace ADSUS_BE.BLL.Auth.DTOs;

/// <summary>
/// UC-02 — bật hoặc tắt đăng nhập sinh trắc học.
///
/// Chỉ có đúng một cờ. Mẫu vân tay/khuôn mặt không bao giờ đi qua đây: nó nằm trong secure
/// enclave của điện thoại và server không bao giờ thấy. UCS ghi rõ cơ chế cụ thể là việc
/// của TDS, nên ở đây giữ ở mức tối giản nhất có thể.
/// </summary>
public class UpdateBiometricRequest
{
    public bool Enabled { get; set; }
}
