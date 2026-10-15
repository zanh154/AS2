# Dump database

`aives_20261002.sql` — dump đầy đủ (schema + dữ liệu) xuất ngày 02/10/2026 từ
`myteam-prn202.c.aivencloud.com`. Đã bao gồm toàn bộ migration ở thư mục cha.

Đây là **cách duy nhất để dựng database mới** — các file `2026*.sql` ở thư mục cha chỉ nâng
cấp schema đã tồn tại chứ không tạo được từ đầu.

```bash
mysql -u <user> -p -e "CREATE DATABASE aives CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -u <user> -p aives < database/dump/aives_20261002.sql
```

Dump không chứa lệnh `USE`/`CREATE DATABASE` nên import vào database đặt tên gì cũng được.

Hướng dẫn đầy đủ và cách xuất dump mới: [../README.md](../README.md).
