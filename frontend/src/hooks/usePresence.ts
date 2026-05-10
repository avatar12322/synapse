import { useEffect, useRef, useState } from 'react';
import { WS_BASE_URL } from '@/lib/config';

interface PresenceUpdate {
  userId: number;
  missionId: number;
  latitude: number;
  longitude: number;
  timestamp: string;
}

export function usePresence(missionId: number, currentUserId: number) {
  const [partnerLocation, setPartnerLocation] = useState<{ lat: number, lng: number } | null>(null);
  const [isPartnerNearby, setIsPartnerNearby] = useState(false);
  const wsRef = useRef<WebSocket | null>(null);

  useEffect(() => {
    if (!missionId || !currentUserId) return;

    const ws = new WebSocket(`${WS_BASE_URL}/ws/presence`);
    wsRef.current = ws;

    ws.onopen = () => {
      console.log('Presence WebSocket connected');
    };

    ws.onmessage = (event) => {
      try {
        const data: PresenceUpdate = JSON.parse(event.data);
        if (data.userId !== currentUserId) {
          setPartnerLocation({ lat: data.latitude, lng: data.longitude });
        }
      } catch (err) {
        console.error('Failed to parse presence update', err);
      }
    };

    ws.onclose = () => {
      console.log('Presence WebSocket disconnected');
    };

    // Send my location every 5 seconds
    const interval = setInterval(() => {
      if (ws.readyState === WebSocket.OPEN) {
        navigator.geolocation.getCurrentPosition((pos) => {
          const update: PresenceUpdate = {
            userId: currentUserId,
            missionId,
            latitude: pos.coords.latitude,
            longitude: pos.coords.longitude,
            timestamp: new Date().toISOString()
          };
          ws.send(JSON.stringify(update));
        });
      }
    }, 5000);

    return () => {
      clearInterval(interval);
      ws.close();
    };
  }, [missionId, currentUserId]);

  // Simple "nearby" logic (e.g. within 50m)
  useEffect(() => {
    if (!partnerLocation) {
      setIsPartnerNearby(false);
      return;
    }

    navigator.geolocation.getCurrentPosition((pos) => {
      const myLat = pos.coords.latitude;
      const myLng = pos.coords.longitude;
      
      // Rough distance calculation (Haversine simplified for small distances)
      const dLat = (partnerLocation.lat - myLat) * Math.PI / 180;
      const dLng = (partnerLocation.lng - myLng) * Math.PI / 180;
      const a = Math.sin(dLat/2) * Math.sin(dLat/2) +
                Math.cos(myLat * Math.PI / 180) * Math.cos(partnerLocation.lat * Math.PI / 180) *
                Math.sin(dLng/2) * Math.sin(dLng/2);
      const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));
      const distance = 6371e3 * c; // in meters

      setIsPartnerNearby(distance < 50);
    });
  }, [partnerLocation]);

  return { partnerLocation, isPartnerNearby };
}
