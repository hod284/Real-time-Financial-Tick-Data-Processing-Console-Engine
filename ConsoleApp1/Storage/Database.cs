using ConsoleApp1.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace ConsoleApp1.Storage
{
    // SQLite 연결 생성 + 테이블 생성 담당
    //  - trades        : 체결 전부
    //  - candles       : 1분봉 (같은 봉은 덮어쓰기)
    //  - ticker_latest : 종목별 마지막 티커 1줄 (이력 X)
    //  - alerts        : 분석 알림 이력
    //  - 호가는 100ms마다 와서 저장 안 함
    // SQLite에는 decimal이 없어서 가격/수량은 REAL(double)로 저장
    public sealed class Database
    {
        private readonly string _connectionString;
        public string FilePath { get; }

        //IOptions
        //    1. 왜 쓰는가?
        //    - appsettings.json의 날것(텍스트)인 JSON 데이터를 오타 걱정 없는 
        //      강력한 형식(Strongly-typed)의 C# 클래스/레코드 객체로 안전하게 매핑하기 위해 사용.
        //    - 코드 내 하드코딩을 방지하여 유지보수성, 보안성(비밀번호 분리), 배포 편의성을 극대화함.
        //
        // 2. 어떻게 작동하는가? (.NET 내부 마법)
        //    - Program.cs의 'services.Configure<T>(Configuration.GetSection("..."))' 등록 코드를 통해,
        //      .NET 엔진이 JSON의 Key 이름과 C# 객체의 Property 이름을 대조하여 자동으로 값을 채워넣음.
        //    - 이후 의존성 주입(DI) 시스템에 의해 값이 채워진 완제품 상자가 이 생성자로 배달됨.
        //
        // 3. 어떻게 꺼내 쓰는가?
        //    - 주입받은 options 변수의 '.Value' 속성을 호출하면 맵핑된 실제 데이터 객체가 튀어나옴.
        // =================================================================================
        public Database(IOptions<MarketOptions> options)
        {
            FilePath = Path.GetFullPath(options.Value.DbPath);
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = FilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,   // 파일 없으면 새로 만듦
            }.ToString();
        }
        public SqliteConnection Open()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            // 다른 연결이 쓰는 중이면 최대 5초 기다림 ('database is locked' 방지)
            conn.Execute("PRAGMA busy_timeout = 5000;");
            return conn;
        }

        // 프로그램 시작 시 1번 호출 (IF NOT EXISTS라 여러 번 불러도 안전)
        public void Initialize()
        {
            using var conn = Open();
            conn.Execute(Schema);
        }

        private const string Schema = """
            PRAGMA journal_mode = WAL;      -- 쓰는 동안에도 다른 연결이 읽을 수 있음
            PRAGMA synchronous = NORMAL;    -- WAL에서 권장값, 커밋 속도 향상

            CREATE TABLE IF NOT EXISTS trades (
                exchange       TEXT    NOT NULL,
                symbol         TEXT    NOT NULL,
                trade_id       INTEGER NOT NULL,
                trade_time_ms  INTEGER NOT NULL,
                recv_time_ms   INTEGER NOT NULL,
                price          REAL    NOT NULL,
                qty            REAL    NOT NULL,
                side           TEXT    NOT NULL,
                PRIMARY KEY (exchange, symbol, trade_id)      -- 같은 체결이 또 와도 1번만 저장
            ) WITHOUT ROWID;
            CREATE INDEX IF NOT EXISTS ix_trades_time ON trades (exchange, symbol, trade_time_ms);

            CREATE TABLE IF NOT EXISTS candles (
                exchange      TEXT    NOT NULL,
                symbol        TEXT    NOT NULL,
                interval      TEXT    NOT NULL,
                open_time_ms  INTEGER NOT NULL,
                open          REAL    NOT NULL,
                high          REAL    NOT NULL,
                low           REAL    NOT NULL,
                close         REAL    NOT NULL,
                volume        REAL    NOT NULL,
                is_closed     INTEGER NOT NULL,
                updated_ms    INTEGER NOT NULL,
                PRIMARY KEY (exchange, symbol, interval, open_time_ms)
            ) WITHOUT ROWID;

            -- [선택] 대시보드에서 현재가 보여줄 거 아니면 빼도 됨
            CREATE TABLE IF NOT EXISTS ticker_latest (
                exchange         TEXT    NOT NULL,
                symbol           TEXT    NOT NULL,
                last_price       REAL    NOT NULL,
                change_rate_24h  REAL    NOT NULL,
                volume_24h       REAL    NOT NULL,
                high_24h         REAL    NOT NULL,
                low_24h          REAL    NOT NULL,
                exch_time_ms     INTEGER NOT NULL,
                PRIMARY KEY (exchange, symbol)
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS alerts (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                exchange   TEXT    NOT NULL,
                symbol     TEXT    NOT NULL,
                rule       TEXT    NOT NULL,
                message    TEXT    NOT NULL,
                value      REAL    NOT NULL,
                threshold  REAL    NOT NULL,
                time_ms    INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_alerts_time ON alerts (time_ms);
            """;
    }
}