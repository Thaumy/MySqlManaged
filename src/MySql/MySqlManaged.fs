﻿namespace DbManaged.MySql

open System
open System.Data
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Data.Common
open System.Threading.Tasks
open MySql.Data.MySqlClient
open fsharper.op
open fsharper.typ
open fsharper.op.Alias
open fsharper.op.Coerce
open DbManaged
open DbManaged.ext
open DbManaged.AnySql
open DbManaged.MySql.ext

/// MySql数据库管理器
type MySqlManaged private (pool: IDbConnPoolAsync, syncSpan: u32) as self =

    let queryQueue = StringBuilder()

    let _ =
        fun _ ->
            while true do
                (self :> IDbQueryQueue).forceLeftQueuedQuery ()
                Thread.Sleep(i32 syncSpan)
        |> Task.Run

    /// 以连接信息构造
    new(msg) =
        let pool =
            new AnySqlConnPool<MySqlConnection>(msg, 32u)

        new MySqlManaged(pool, 0u)
    /// 以连接信息构造，并指定使用的数据库
    new(msg, database: string) =
        let pool =
            new AnySqlConnPool<MySqlConnection>(msg, database, 32u)

        new MySqlManaged(pool, 0u)
    /// 以连接信息构造，并指定使用的数据库和连接池大小
    new(msg, database, poolSize) =
        let pool =
            new AnySqlConnPool<MySqlConnection>(msg, database, poolSize)

        new MySqlManaged(pool, 0u)
    /// 以连接信息构造，并指定使用的数据库和连接池大小，附加队列同步间隔
    new(msg, database, poolSize, syncSpan) =
        let pool =
            new AnySqlConnPool<MySqlConnection>(msg, database, poolSize)

        new MySqlManaged(pool, syncSpan)

    interface IDbQueryQueue with

        member self.queueQuery(sql: string) =
            lock self (fun _ -> sql |> queryQueue.Append |> ignore)

        member self.forceLeftQueuedQuery() =
            fun _ ->
                let sql = queryQueue.ToString()
                queryQueue.Clear() |> ignore

                (self :> IDbManaged).executeAny sql |> unwrap
                <| always true
                |> ignore
            |> lock self

    interface IDisposable with
        member self.Dispose() =
            (self :> IDbQueryQueue).forceLeftQueuedQuery ()
            pool.Dispose()

    interface IDbManaged with

        /// 查询到表
        override self.executeSelect sql =
            pool.hostConnection
            <| fun conn' ->
                let conn: MySqlConnection = coerce conn'
                let table = new DataTable()

                table
                |> (new MySqlDataAdapter(sql, conn)).Fill
                |> ignore

                table
        /// 参数化查询到表
        override self.executeSelect(sql, paras: (string * 't) list) =
            let paras' =
                foldMap (fun (k: string, v) -> List' [ MySqlParameter(k, v :> obj) ]) paras
                |> unwrap

            (self :> IDbManaged)
                .executeSelect (sql, paras'.toArray ())
        /// 参数化查询到表
        override self.executeSelect(sql, para: #DbParameter array) =
            pool.hostConnection
            <| fun conn' ->
                let conn: MySqlConnection = coerce conn'

                conn.hostCommand
                <| fun cmd' ->
                    let cmd: MySqlCommand = coerce cmd'

                    let table = new DataTable()

                    cmd.CommandText <- sql
                    cmd.Parameters.AddRange para //添加参数

                    (new MySqlDataAdapter(cmd)).Fill table |> ignore

                    table


        /// 查询到第一个值
        override self.getFstVal sql =
            pool.hostConnection
            <| fun conn ->
                conn.hostCommand
                <| fun cmd ->
                    cmd.CommandText <- sql

                    //如果结果集为空，ExecuteScalar返回null
                    match cmd.ExecuteScalar() with
                    | null -> None
                    | x -> Some x
        /// 参数化查询到第一个值
        override self.getFstVal(sql, paras: (string * 't) list) =
            let paras' =
                foldMap (fun (k: string, v) -> List' [ MySqlParameter(k, v :> obj) ]) paras
                |> unwrap

            (self :> IDbManaged)
                .getFstVal (sql, paras'.toArray ())
        /// 参数化查询到第一个值
        override self.getFstVal(sql, para: #DbParameter array) =
            pool.hostConnection
            <| fun conn ->
                conn.hostCommand
                <| fun cmd ->
                    cmd.CommandText <- sql
                    cmd.Parameters.AddRange para

                    //如果结果集为空，ExecuteScalar返回null
                    match cmd.ExecuteScalar() with
                    | null -> None
                    | x -> Some x
        /// 参数化查询到第一个值
        override self.getFstVal(table: string, targetKey: string, (whereKey: string, whereKeyVal: 'V)) =
            pool.hostConnection
            <| fun conn ->
                conn.hostCommand
                <| fun cmd ->
                    cmd.CommandText <- $"SELECT `{targetKey}` FROM `{table}` WHERE `{whereKey}` = ?whereKeyVal"

                    cmd.Parameters.AddRange [| MySqlParameter("whereKeyVal", whereKeyVal) |]

                    //如果结果集为空，ExecuteScalar返回null
                    match cmd.ExecuteScalar() with
                    | null -> None
                    | x -> Some x
        /// 查询到第一行
        override self.getFstRow sql =
            (self :> IDbManaged).executeSelect sql
            >>= fun t ->
                    Ok
                    <| match t.Rows with
                       //仅当行数非零时有结果
                       | rows when rows.Count <> 0 -> Some rows.[0]
                       | _ -> None
        /// 参数化查询到第一行
        override self.getFstRow(sql, paras: (string * 't) list) =
            let paras' =
                foldMap (fun (k: string, v) -> List' [ MySqlParameter(k, v :> obj) ]) paras
                |> unwrap

            (self :> IDbManaged)
                .getFstRow (sql, paras'.toArray ())
        /// 参数化查询到第一行
        override self.getFstRow(sql, paras: #DbParameter array) =
            (self :> IDbManaged).executeSelect (sql, paras)
            >>= fun t ->
                    Ok
                    <| match t.Rows with
                       //仅当行数非零时有结果
                       | rows when rows.Count <> 0 -> Some rows.[0]
                       | _ -> None

        /// 查询到指定列
        override self.getCol(sql, key: string) =
            (self :> IDbManaged).executeSelect sql
            >>= fun t -> getColFromByKey (t, key) |> Ok

        /// 参数化查询到指定列
        override self.getCol(sql, key: string, paras: (string * 't) list) =
            let paras' =
                foldMap (fun (k: string, v) -> List' [ MySqlParameter(k, v :> obj) ]) paras
                |> unwrap

            (self :> IDbManaged)
                .getCol (sql, key, paras'.toArray ())
        /// 参数化查询到指定列
        override self.getCol(sql, key: string, paras: #DbParameter array) =
            (self :> IDbManaged).executeSelect (sql, paras)
            >>= fun t -> Ok <| getColFromByKey (t, key)


        /// 查询到指定列
        override self.getCol(sql, index: u32) =
            (self :> IDbManaged).executeSelect sql
            >>= fun t -> getColFromByIndex (t, index) |> Ok

        /// 参数化查询到指定列
        override self.getCol(sql, index: u32, paras: (string * 't) list) =
            let paras' =
                foldMap (fun (k: string, v) -> List' [ MySqlParameter(k, v :> obj) ]) paras
                |> unwrap

            (self :> IDbManaged)
                .getCol (sql, index, paras'.toArray ())
        /// 参数化查询到指定列
        override self.getCol(sql, index: u32, paras: #DbParameter array) =
            (self :> IDbManaged).executeSelect (sql, paras)
            >>= fun t -> Ok <| getColFromByIndex (t, index)


        //partial...



        override self.executeAny sql =
            pool.getConnection ()
            >>= fun conn ->
                    let result = conn.executeAny sql

                    lazy (pool.recycleConnection conn) |> result |> Ok

        override self.executeAny(sql, paras: (string * 't) list) =
            let paras' =
                foldMap (fun (k: string, v) -> List' [ MySqlParameter(k, v :> obj) ]) paras
                |> unwrap

            (self :> IDbManaged)
                .executeAny (sql, paras'.toArray ())

        override self.executeAny(sql, paras) =
            pool.getConnection ()
            >>= fun conn ->
                    let result = conn.executeAny (sql, paras)

                    lazy (pool.recycleConnection conn) |> result |> Ok


        override self.executeUpdate(table, (setKey, setKeyVal), (whereKey, whereKeyVal)) =
            pool.getConnection ()
            >>= fun conn' ->
                    let conn: MySqlConnection = coerce conn'

                    let result =
                        conn.executeUpdate (table, (setKey, setKeyVal), (whereKey, whereKeyVal))

                    lazy (pool.recycleConnection conn) |> result |> Ok

        override self.executeUpdate(table, key, newValue, oldValue) =
            pool.getConnection ()
            >>= fun conn' ->
                    let conn: MySqlConnection = coerce conn'


                    let result =
                        conn.executeUpdate (table, key, newValue, oldValue)

                    lazy (pool.recycleConnection conn) |> result |> Ok



        override self.executeInsert table pairs =
            pool.getConnection ()
            >>= fun conn' ->
                    let conn: MySqlConnection = coerce conn'

                    let result = conn.executeInsert table pairs

                    lazy (pool.recycleConnection conn) |> result |> Ok

        override self.executeDelete table (whereKey, whereKeyVal) =
            pool.getConnection ()
            >>= fun conn' ->
                    let conn: MySqlConnection = coerce conn'

                    let result =
                        conn.executeDelete table (whereKey, whereKeyVal)

                    lazy (pool.recycleConnection conn) |> result |> Ok

    interface IDbManagedAsync with
        /// TODO exp async api
        member self.executeAnyAsync sql =
            let r = pool.getConnectionAsync().Result

            r
            >>= fun conn ->
                    let result = conn.executeAnyAsync sql

                    lazy (pool.recycleConnectionAsync conn |> ignore)
                    |> result
                    |> Ok
        /// TODO exp async api
        member self.executeAnyAsync(sql, paras: (string * 't) list) =
            let paras' =
                foldMap (fun (k: string, v) -> List' [ MySqlParameter(k, v :> obj) ]) paras
                |> unwrap

            (self :> IDbManagedAsync)
                .executeAnyAsync (sql, paras'.toArray ())
        /// TODO exp async api
        member self.executeAnyAsync(sql, paras) =
            let r = pool.getConnectionAsync().Result

            r
            >>= fun conn ->
                    let result = conn.executeAnyAsync (sql, paras)

                    lazy (pool.recycleConnectionAsync conn |> ignore)
                    |> result
                    |> Ok
