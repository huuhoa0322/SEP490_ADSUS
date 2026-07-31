package com.adsus.adsus_mobile

import io.flutter.embedding.android.FlutterFragmentActivity

// Phải kế thừa FlutterFragmentActivity, KHÔNG phải FlutterActivity.
//
// Gói local_auth dựng hộp thoại sinh trắc học bằng BiometricPrompt của AndroidX, mà cái đó
// đòi một FragmentActivity để gắn vào. Để nguyên FlutterActivity thì build vẫn qua, chạy
// vẫn lên, nhưng đúng lúc gọi quét vân tay sẽ văng lỗi ở tầng native.
class MainActivity : FlutterFragmentActivity()
