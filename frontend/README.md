# ReGreen Frontend

React, TypeScript ve Vite tabanlı ReGreen kullanıcı arayüzü.

## Çalıştırma

```bash
npm install
npm run dev
```

## Veri kaynağı

`.env.example` dosyasını `.env.local` olarak kopyalayın.

- `VITE_SERVICE_MODE=mock`: Backend olmadan temel mock servis
- `VITE_SERVICE_MODE=http`: Gerçek ReGreen API
- `VITE_API_BASE_URL`: API origin'i; örneğin `http://localhost:5000`

UI bileşenleri veri kaynağına doğrudan erişmez. Tüm erişim `FireService`
arayüzü üzerinden `MockFireService` veya `HttpFireService` ile yapılır.
