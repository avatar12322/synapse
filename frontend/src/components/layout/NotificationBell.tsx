"use client";

import { useState, useEffect } from 'react';
import { Bell, CheckCheck, Map, AlertCircle, UserPlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { notificationApi } from '@/lib/api';
import { authStorage } from '@/lib/auth';

type NotificationType = 0 | 1 | 2 | 3 | 4;

interface Notification {
  id: number;
  title: string;
  message: string;
  type: NotificationType;
  isRead: boolean;
  createdAt: string;
}

function getIcon(type: NotificationType) {
  switch (type) {
    case 0: return <Map className="h-4 w-4 text-purple-500" />;
    case 1: return <CheckCheck className="h-4 w-4 text-green-500" />;
    case 2: return <AlertCircle className="h-4 w-4 text-red-500" />;
    case 3: return <UserPlus className="h-4 w-4 text-blue-500" />;
    default: return <Bell className="h-4 w-4 text-gray-400" />;
  }
}

export default function NotificationBell() {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [isOpen, setIsOpen] = useState(false);

  const unread = notifications.filter(n => !n.isRead).length;

  useEffect(() => {
    if (!authStorage.getUser()) return;
    const load = async () => {
      try {
        const data = await notificationApi.getUnread();
        setNotifications(Array.isArray(data) ? data : []);
      } catch { /* ignore */ }
    };
    load();
    const interval = setInterval(load, 10000);
    return () => clearInterval(interval);
  }, []);

  const markAllRead = async () => {
    await notificationApi.markAllRead();
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
  };

  if (!authStorage.getUser()) return null;

  return (
    <div className="relative">
      <Button variant="ghost" size="icon" onClick={() => setIsOpen(!isOpen)}
        className="relative text-purple-300 hover:text-white hover:bg-purple-500/20">
        <Bell className="h-5 w-5" />
        {unread > 0 && (
          <Badge className="absolute -top-1 -right-1 h-5 w-5 rounded-full bg-red-500 text-white text-xs flex items-center justify-center p-0 min-w-[20px]">
            {unread > 9 ? '9+' : unread}
          </Badge>
        )}
      </Button>

      {isOpen && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setIsOpen(false)} />
          <div className="absolute right-0 top-full mt-2 w-80 bg-slate-800 border-2 border-purple-500/30 rounded-lg shadow-xl z-50 max-h-96 overflow-hidden">
            <Card className="border-0 bg-transparent">
              <CardHeader className="pb-3">
                <div className="flex items-center justify-between">
                  <CardTitle className="text-purple-300 text-lg">Notifications</CardTitle>
                  {unread > 0 && (
                    <Button variant="ghost" size="sm" onClick={markAllRead}
                      className="text-purple-400 hover:text-white text-xs">
                      <CheckCheck className="h-3 w-3 mr-1" /> Mark all read
                    </Button>
                  )}
                </div>
              </CardHeader>
              <CardContent className="pt-0 max-h-64 overflow-y-auto">
                {notifications.length === 0 ? (
                  <div className="text-center py-8 text-gray-400">
                    <Bell className="h-12 w-12 mx-auto mb-2 opacity-50" />
                    <p>No notifications yet</p>
                  </div>
                ) : (
                  <div className="space-y-2">
                    {notifications.map(n => (
                      <div key={n.id}
                        className={`p-3 rounded-lg border ${n.isRead ? 'bg-slate-700/30 border-slate-600/30 opacity-75' : 'bg-slate-700 border-purple-500/30'}`}>
                        <div className="flex items-start gap-3">
                          <div className="flex-shrink-0 mt-0.5">{getIcon(n.type)}</div>
                          <div className="flex-1 min-w-0">
                            <h4 className="font-bold text-sm text-white truncate">{n.title}</h4>
                            <p className="text-xs text-gray-300 mt-1 line-clamp-2">{n.message}</p>
                            <p className="text-xs text-gray-500 mt-2">{new Date(n.createdAt).toLocaleString()}</p>
                          </div>
                          {!n.isRead && <div className="w-2 h-2 bg-blue-500 rounded-full flex-shrink-0 mt-1" />}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>
          </div>
        </>
      )}
    </div>
  );
}
