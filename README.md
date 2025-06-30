# Gemini Powered Pet Simulator

Prototipe game simulasi hewan peliharaan 2D top-down di Unity di mana setiap NPC memiliki kepribadian, kebutuhan, dan interaksi yang unik, didukung oleh Google Gemini AI.

---

## 📜 Tentang Proyek

Proyek ini adalah implementasi dari game simulasi di mana pemain bertugas merawat sekelompok hewan peliharaan AI. Setiap hewan memiliki sifat yang berbeda (sosial, malas, bersih) yang dihasilkan oleh AI, yang secara langsung memengaruhi perilaku dan kebutuhan mereka. Game ini endless dan akan berakhir jika semua hewan peliharaan mati.

Tujuan utama dari repositori ini adalah untuk menjadi contoh bagaimana mengintegrasikan Large Language Model (LLM) seperti Gemini ke dalam game Unity untuk menciptakan perilaku NPC yang dinamis dan tak terduga.

## ✨ Fitur Utama

*   **Kepribadian Dinamis dari AI**: Setiap NPC memiliki nama, tingkat sosial, kemalasan, dan kebersihan yang digenerate oleh Gemini.
*   **Sistem Kebutuhan (Needs System)**: NPC memiliki atribut lapar, haus, energi, kebersihan, dan kebahagiaan yang terus menurun.
*   **AI Berbasis State Machine**: NPC secara mandiri memutuskan tindakan apa yang harus dilakukan (makan, minum, istirahat, mandi, bermain, atau wandering).
*   **Interaksi Antar NPC**: Interaksi antar NPC dihasilkan oleh Gemini, dengan hasil positif atau negatif yang memengaruhi kebahagiaan mereka.
*   **Mode Pengembangan Offline**: Termasuk toggle untuk menjalankan game tanpa memanggil API, berguna untuk development dan menghemat kuota.

## 🚀 Memulai

Untuk menjalankan proyek ini di komputer Anda, ikuti langkah-langkah berikut.

### Prasyarat

*   **Unity Hub** dan **Unity Editor (Versi 2021.3.x atau lebih baru)**.
*   **API Key Google Gemini**. Anda bisa mendapatkannya dari [Google AI Studio](https://ai.google.dev/).

### Instalasi & Setup

1.  **Clone repositori ini:**
    ```bash
    git clone https://github.com/NamaAnda/Gemini-Pet-Simulator.git
    ```
2.  **Buka proyek di Unity Hub:**
    *   Klik "Open" -> "Add project from disk".
    *   Arahkan ke folder yang baru saja Anda clone.
3.  **Konfigurasi API Key (Langkah Penting):**
    *   Di dalam folder `Assets` di Unity, cari file bernama `GeminiKey.template.json`.
    *   Buat salinan dari file ini di folder yang sama.
    *   Ganti nama salinan tersebut menjadi **`GeminiKey.json`**.
    *   Buka `GeminiKey.json` dan ganti placeholder `"GANTI_DENGAN_API_KEY_GEMINI_ANDA"` dengan API Key Gemini Anda yang valid.
4.  **Assign File Kunci di Unity:**
    *   Pilih GameObject yang memiliki skrip `UnityAndGeminiV3` di Hierarchy (biasanya bernama `GameManager` atau sejenisnya).
    *   Di Inspector, cari komponen `Unity And Gemini V3`.
    *   Seret file **`GeminiKey.json`** dari folder `Assets` ke field `Gemini Key Json File`.
5.  **Buka Scene Utama:**
    *   Di Project window, cari scene utama (misalnya `SampleScene`) dan buka.
6.  **Tekan Tombol Play!**

## 🎮 Cara Menggunakan

*   **Mode Online**: Untuk menggunakan Gemini, pilih objek `GameManager` di Hierarchy. Di Inspector, **hilangkan centang** pada `Use Offline Development Mode`.
*   **Mode Offline (Default)**: Untuk development tanpa menggunakan kuota API, **pastikan** `Use Offline Development Mode` **tercentang**. Game akan menggunakan data acak lokal untuk kepribadian dan interaksi.

---

## ⚖️ Lisensi

Didistribusikan di bawah Lisensi MIT. Lihat `LICENSE` untuk informasi lebih lanjut.
