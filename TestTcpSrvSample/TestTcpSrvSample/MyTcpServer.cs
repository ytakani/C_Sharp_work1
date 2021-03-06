﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections;           // add ArrayList


namespace TestTcpSrvSample
{

    /**
    *  @brief      MyTcpServer
    *  @note       独自 TCP Server (TcpListener使用)
    */
    class MyTcpServer
    {
        TcpListener server;
        Thread srvThread = null;
        ThreadStart srvThreadDelegate;
        ArrayList clients;

        bool stopReqFlag;

        /**
         *  @brief       startProc
         *  @param[in]   string   ipadd   ServerIPAddress (いらないかも )
         *  @param[in]   int      portno  PorNo
         *  @param[in]   int      queueMAx    最大Client待ち数  
         *  @return      bool     true:起動OK
         *  @note        TcpServer起動
         */
        public bool startProc(string ipadd, int portno, int queueMax)
        {
            stopReqFlag = false;
            clients = new ArrayList();

            if ((queueMax < 1) || (queueMax > 10))
                return false;

            //System.Net.IPAddress ipAdd = System.Net.IPAddress.Parse(ipadd);
            //server = new TcpListener(ipAdd, portno);
            server = new TcpListener(System.Net.IPAddress.Any, portno);
            //server.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1000);
            SetKeepAlive(server.Server, 1000, 1000);
            try
            {
                server.Start(queueMax);
            }
            catch (SocketException er)
            {
                Console.WriteLine("Server start error."+ er.Message);
                return false;
            }

            // スレッド起動
            srvThreadDelegate = new ThreadStart(ServerThreadFuction);
            srvThread = new Thread(srvThreadDelegate);
            srvThread.Start();

            return true;
        }

        /**
         *  @brief      SetKeepAlive
         *  @param[in]  sock    socket
         *  @param[in]  int     time
         *  @param[in]  int     interval
         *  @return     void
         *  @note          Socket KeepAliveの設定
         */
        private void SetKeepAlive(Socket sock, int time, int interval)
        {
            byte[] inValue  = new byte[12];
            byte[] outValue = new byte[4];
            inValue  = Enumerable.Repeat<byte>(0x00, 12).ToArray();
            outValue = Enumerable.Repeat<byte>(0x00,  4).ToArray();

            BitConverter.GetBytes(1).CopyTo(inValue, 0);    // KeepAliveOn/Off
            BitConverter.GetBytes(time).CopyTo(inValue, 4); // KeepAliveTime
            BitConverter.GetBytes(interval).CopyTo(inValue, 8); // KeepAliveInterval
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            sock.IOControl(IOControlCode.KeepAliveValues, inValue, outValue);
        }

        /**
         *  @brief       endProc
         *  @param[in]   none
         *  @return      void
         *  @note        TcpServerスレッド終了開始
         */
        public void endProc()
        {
            stopReqFlag = true;     // Server処理スレッド終了要求
            if(srvThread!=null)
                srvThread.Join();   // Server処理スレッド終了待ち
        }

        /**
         *  @brief       ServerThreadFuction
         *  @param[in]   none
         *  @return      void
         *  @note        TcpServerスレッド処理
         */
        void ServerThreadFuction()
        {
            while (true)
            {
                Thread.Sleep(1);

                if (stopReqFlag == true)        // 終了要求時
                {
                    // 各Client停止要求後
                    allClientEndProc();
                    break;
                }
                if (server.Pending() == true)   // true:Clientが接続してきた
                {
                    try
                    {
                        ServerSideTcpClient srvSideClient = new ServerSideTcpClient();
                        srvSideClient.setTcpClient(server.AcceptTcpClient());           // Accept待ち
                        clients.Add(srvSideClient);                                     // Clinetリストに追加
                        srvSideClient.startProc();                                      // Clientとの通信用スレッド起動
                    }
                    catch (System.IO.IOException ioerr)  // 切断検知
                    {
                        Console.WriteLine(ioerr.Message);
                        allClientEndProc();
                    }
                }
                
                // 終了した ServerSideTcpClient クラス解放
                clientArrayMemberClear();
 
            }
        }

        /**
         *  @brief       clientArrayMemberClear
         *  @param[in]   none
         *  @return      void
         *  @note        Client保持 ArraList から 切断したClient破棄
         */
        void clientArrayMemberClear() {
            for (int i = clients.Count - 1; i >= 0; i--)
            {
                ServerSideTcpClient srvSideClient = (ServerSideTcpClient)clients[i];
                if (srvSideClient.getEndFlag() == true)
                {
                    clients.Remove(clients[i]); // 要素の削除
                    srvSideClient.Dispose();
                }
            }
        }

        /**
         *  @brief       allClientEndProc
         *  @param[in]   none
         *  @return      void
         *  @note        接続している全Client切断、Client処理スレッド終了依頼
         */
        void allClientEndProc()
        {
            for (int i = clients.Count - 1; i >= 0; i--)
            {
                ServerSideTcpClient srvSideClient = (ServerSideTcpClient)clients[i];
                clients.Remove(clients[i]);     // 要素の削除
                srvSideClient.endProc();        // Clientとの通信処理スレッド終了待ち
                srvSideClient.Dispose();        // Clientとの通信処理クラス破棄
            }
        }

    }
}
