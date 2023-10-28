from bottle import route, run, template, HTTPResponse


@route('/', method='GET')  # or @get('/')
def index():
    return template('index', username="ダミーサーバ")


@route('/api/<param>', method='GET')
def api(param):
    body = {"status": "OK", "request": param, "message": "hello world"}
    r = HTTPResponse(status=200, body=body)
    r.set_header("Content-Type", "application/json")
    return r


@route('/image')
def image():
    # curl http://localhost:8080/image --output cat.jpeg
    with open('./assets/images/cat.jpeg', 'rb') as img:
        body = img.read()
    headers = {'Content-Type': 'image/jpeg'}
    return HTTPResponse(status=200, body=body, headers=headers)


# プロセスの起動
if __name__ == "__main__":
    run(host='localhost', port=8080, reloader=True, debug=True)
