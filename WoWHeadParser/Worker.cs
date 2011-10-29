﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Threading;

namespace WoWHeadParser
{
    public class Worker : IDisposable
    {
        private ParsingType _type;
        private int _start;
        private int _end;
        private int _entry;
        private object _threadLock;
        private string _address;
        private Queue<Block> _pages;
        private BackgroundWorker _background;
        private List<Requests> _requests;
        private List<uint> _entries;

        private const int PercentProgress = 1;

        public Queue<Block> Pages
        {
            get { return _pages; }
        }

        public Worker(int start, int end, string address, BackgroundWorker background)
        {
            _type = ParsingType.PARSING_TYPE_MULTIPLE;
            _end = end;
            _start = start;
            _address = address;
            _background = background;
            _threadLock = new object();
            _pages = new Queue<Block>();
            _background.DoWork += new DoWorkEventHandler(DownloadInitial);
            _requests = new List<Requests>();
        }

        public Worker(int value, string address, BackgroundWorker background)
        {
            _type = ParsingType.PARSING_TYPE_SINGLE;
            _entry = value;
            _address = address;
            _background = background;
            _threadLock = new object();
            _pages = new Queue<Block>();
            _background.DoWork += new DoWorkEventHandler(DownloadInitial);
            _requests = new List<Requests>();
        }

        public Worker(List<uint> entries, string address, BackgroundWorker background)
        {
            _type = ParsingType.PARSING_TYPE_LIST;
            _entries = entries;
            _address = address;
            _background = background;
            _threadLock = new object();
            _pages = new Queue<Block>();
            _background.DoWork += new DoWorkEventHandler(DownloadInitial);
            _requests = new List<Requests>();
        }

        public void Start()
        {
            //Starting work on a different thread to prevent MainForm freezing
            _background.RunWorkerAsync();
        }

        void DownloadInitial(object sender, DoWorkEventArgs e)
        {
            switch (_type)
            {
                case ParsingType.PARSING_TYPE_SINGLE:
                    {
                        Requests request = new Requests(new Uri(string.Format("{0}{1}", _address, _entry)), _entry);
                        request.Request.BeginGetResponse(new AsyncCallback(RespCallback), request);
                        break;
                    }
                case ParsingType.PARSING_TYPE_MULTIPLE:
                    {
                        for (_entry = _start; _entry < _end; ++_entry)
                        {
                            Requests request = new Requests(new Uri(string.Format("{0}{1}", _address, _entry)), _entry);
                            _requests.Add(request);
                            request.Request.BeginGetResponse(new AsyncCallback(RespCallback), request);

                            Thread.Sleep(500);
                        }
                        break;
                    }
                case ParsingType.PARSING_TYPE_LIST:
                    {
                        foreach (uint entry in _entries)
                        {
                            Requests request = new Requests(new Uri(string.Format("{0}{1}", _address, entry)), (int)entry);
                            _requests.Add(request);
                            request.Request.BeginGetResponse(new AsyncCallback(RespCallback), request);

                            Thread.Sleep(500);
                        }
                        break;
                    }
            }
        }


        private void RespCallback(IAsyncResult iar)
        {
            Requests request = (Requests)iar.AsyncState;
            try
            {
                request.Response = (HttpWebResponse)request.Request.EndGetResponse(iar);
                string text = request.GetContent();
                lock (_threadLock)
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        Block block = new Block(text, (uint)request.Entry);
                        _pages.Enqueue(block);
                    }
                }
            }
            catch (Exception e)
            {
            }
            finally
            {
                request.Dispose();
                if (_background.IsBusy)
                    _background.ReportProgress(PercentProgress);
            }
        }


        public void Stop()
        {
            _background.CancelAsync();
            foreach (Requests request in _requests)
                request.Dispose();

            Dispose();
        }

        ~Worker()
        {
            Stop();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_background.IsBusy)
                    _background.CancelAsync();
                if (_pages != null)
                    _pages.Clear();
            }
        }
    }
}
