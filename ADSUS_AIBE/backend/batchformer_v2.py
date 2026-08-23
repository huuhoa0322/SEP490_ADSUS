import torch
import torch.nn as nn

class BatchFormerV2(nn.Module):
    """
    BatchFormerV2 module: Khám phá mối liên hệ giữa các mẫu ảnh trong cùng một batch.
    Phiên bản được tinh chỉnh để cắm thẳng vào Backbone/Neck của YOLO.
    """
    def __init__(self, c1, num_heads=4):
        super().__init__()
        self.c1 = c1
        # Sử dụng TransformerEncoder hạng nhẹ (1 layer) trên chiều batch
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=c1, 
            nhead=num_heads, 
            dim_feedforward=c1*2, 
            dropout=0.0, 
            batch_first=True
        )
        self.transformer = nn.TransformerEncoder(encoder_layer, num_layers=1)

    def forward(self, x):
        # x shape: (Batch, Channels, Height, Width)
        if not self.training or x.size(0) <= 1:
            # Lúc test (inference) hoặc batch_size = 1 thì không áp dụng để giữ nguyên tốc độ
            return x
            
        B, C, H, W = x.shape
        # Global Average Pooling để thu mỗi feature map thành 1 vector (B, C)
        pooled = x.mean(dim=[2, 3]) 
        
        # Thêm 1 chiều ảo để đẩy vào Transformer: (1, B, C) 
        # (Ở đây batch của Transformer = 1, sequence length = B)
        pooled = pooled.unsqueeze(0)
        
        # Self-attention học chéo giữa các ảnh trong batch
        out = self.transformer(pooled) # (1, B, C)
        out = out.squeeze(0) # (B, C)
        
        # Reshape lại để scale vào các chiều không gian
        out = out.view(B, C, 1, 1)
        
        # Dùng sigmoid để tạo attention weights (0 -> 1) nhân ngược lại với feature map ban đầu
        scale = torch.sigmoid(out)
        return x * scale

def monkey_patch_ultralytics():
    """
    Tiêm (Inject) class BatchFormerV2 vào thư viện ultralytics lúc runtime.
    Ultralytics parse_model dùng globals()[m] trong file tasks.py,
    nên ta cần ép BatchFormerV2 vào thẳng từ điển toàn cục (namespace) của file đó.
    """
    import ultralytics.nn.modules.block as block_module
    import ultralytics.nn.tasks as tasks
    
    # Gắn module vào block_module (để lưu trữ)
    block_module.BatchFormerV2 = BatchFormerV2
    
    # Ép thẳng vào không gian tên của tasks.py (nơi chứa globals của hàm parse_model)
    tasks.__dict__['BatchFormerV2'] = BatchFormerV2
    
    print("Monkey-patched: Đã tích hợp thành công BatchFormerV2 vào Ultralytics YOLO!")
