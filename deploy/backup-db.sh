#!/usr/bin/env bash
# Gecelik Postgres yedeği -> gzip -> Cloudflare R2 (S3 uyumlu API, rclone ile).
# Kurulum (bir kez):
#   sudo apt install rclone
#   rclone config  # "r2" adında S3-compatible remote, R2 endpoint + access/secret key ile
#   crontab -e     # 0 3 * * * /var/www/fethiyecicekcisi/deploy/backup-db.sh >> /var/log/fethiyecicekcisi-backup.log 2>&1
set -euo pipefail

DB_NAME="yonca_cicekcilik"
DB_USER="yonca_app"
BACKUP_DIR="/var/backups/fethiyecicekcisi"
DATE=$(date +%Y-%m-%d_%H%M)
FILE="$BACKUP_DIR/fethiyecicekcisi_$DATE.sql.gz"
R2_REMOTE="r2:fethiyecicekcisi-backups"
KEEP_DAYS=30

mkdir -p "$BACKUP_DIR"
pg_dump -h 127.0.0.1 -U "$DB_USER" "$DB_NAME" | gzip > "$FILE"

rclone copy "$FILE" "$R2_REMOTE/"

# Yerelde ve R2'de KEEP_DAYS'ten eski yedekleri sil
find "$BACKUP_DIR" -name '*.sql.gz' -mtime +$KEEP_DAYS -delete
rclone delete --min-age "${KEEP_DAYS}d" "$R2_REMOTE/" || true

echo "Yedek tamamlandı: $FILE"
