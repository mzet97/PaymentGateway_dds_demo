#!/usr/bin/env bash
cd /mnt/e/TI/git/tese/PaymentGateway_dds_demo/web/payment_gateway_web
export NODE_ENV=production
exec setsid npx next start -p 3000 >> /tmp/pgw-frontend.log 2>&1
