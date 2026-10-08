#!/bin/zsh
set -eu

# Finderから開いても、必ずこのプロジェクトを起動する。
cd -- "${0:A:h}"
export PATH="/opt/homebrew/bin:/usr/local/bin:$PATH"

# エラー時はTerminalを残し、理由を読めるようにする。
trap 'launcher_exit=$?; if (( launcher_exit != 0 )) && [[ -t 0 ]]; then printf "\n起動できませんでした。上のエラーを確認してください。\nEnterで終了します。"; read -r; fi' EXIT

launcher_url="http://127.0.0.1:5173/"

# 同じゲームが起動済みなら、サーバーを増やさずブラウザだけ開く。
if /usr/bin/curl --fail --silent --max-time 2 "$launcher_url" \
  | /usr/bin/grep -Fq '<title>POLY / GRID — Shape Study</title>'; then
  printf '起動済みのゲームを開きます: %s\n' "$launcher_url"
  /usr/bin/open "$launcher_url"
  exit 0
fi

if ! command -v node >/dev/null || ! command -v npm >/dev/null; then
  printf 'Node.jsとnpmが必要です。Node.jsをインストールしてから、もう一度開いてください。\n' >&2
  exit 1
fi

if ! node -e 'const [major, minor] = process.versions.node.split(".").map(Number); process.exit((major === 20 && minor >= 19) || (major === 22 && minor >= 12) || major >= 24 ? 0 : 1)'; then
  printf 'Node.js 20.19+、22.12+、または24以降が必要です。現在のバージョン: ' >&2
  node --version >&2
  exit 1
fi

if [[ ! -x node_modules/.bin/vite ]]; then
  printf '初回の準備をしています…\n'
  npm ci
fi

printf '\nPOLY / GRIDを起動します。ブラウザが自動で開きます。\n'
printf '遊んでいる間はこのTerminalを開いたままにしてください。終了はControl+Cです。\n\n'
npm run dev -- --host 127.0.0.1 --port 5173 --strictPort --open
