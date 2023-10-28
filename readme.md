# 環境

- windows10
- git bash + python 3.11.6
- visual stdio 2017
- C# .NET Framework 4.7.6

# 使い方

1. プロジェクト取得  
   git bash 起動  
   ``` bash
   $ git clone https://github.com/oya3/cs_http_access_test
   ```
1. vs2017 でcs_appをビルド  
   cs_http_access_test/cs_apps/main.sln 起動しビルド。  
1. ダミーサーバ起動  
   ``` bash
   # プロジェクトルートに移動
   $ cd cs_http_access_test
   # ダミーサーバに移動
   $ cd dummy_server
   # python 3.11.6 インストール済みであること
   $ python -m venv venv
   $ source venv/Scripts/activate
   $ pip install -r requirements.txt
   $ python server.py
   Bottle v0.12.25 server starting up (using WSGIRefServer())...
   Listening on http://localhost:8080/
   Hit Ctrl-C to quit.
   
   <frozen importlib._bootstrap>:1047: ImportWarning: _ImportRedirect.find_spec() not found; falling back to find_module()
   ```
1. cs_app を起動  
   起動後、ボタン押下でダミーサーバに接続  
