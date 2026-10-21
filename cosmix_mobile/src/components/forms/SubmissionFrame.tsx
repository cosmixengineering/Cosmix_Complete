import React, { useEffect, useRef, useState } from 'react';
import { BackHandler, Text, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { Button, Notice, Screen } from '../Controls';
import { RequestFeedback } from '../RequestFeedback';
import { useServiceMutation } from '../../hooks/useServiceMutation';
import type { RootStackParams } from '../../navigation/types';
import type { ServiceKey } from '../../types/selfService';
import { layout } from '../../theme/theme';
import { describeError } from '../../utils/errors';

export type PreparedRequest = { body: unknown; summary: string[] };

export function SubmissionFrame({
  service,
  title,
  description,
  route,
  revision,
  ready = true,
  build,
  onSubmitted,
  children,
}: {
  service: ServiceKey;
  title: string;
  description: string;
  route: string;
  revision: string;
  ready?: boolean;
  build(): Promise<PreparedRequest>;
  onSubmitted?(): Promise<void>;
  children(disabled: boolean): React.ReactNode;
}): React.JSX.Element {
  const navigation =
    useNavigation<NativeStackNavigationProp<RootStackParams>>();
  const mutation = useServiceMutation();
  const [prepared, setPrepared] = useState<PreparedRequest | null>(null);
  const [preparing, setPreparing] = useState(false);
  const [error, setError] = useState('');
  const [submitted, setSubmitted] = useState(false);
  const currentRevision = useRef(revision);
  const mounted = useRef(true);
  const flight = useRef(false);
  const busy = preparing || mutation.busy;
  currentRevision.current = revision;
  useEffect(() => {
    setPrepared(null);
    setError('');
  }, [revision]);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  useEffect(() => {
    navigation.setOptions({ gestureEnabled: !busy, headerBackVisible: !busy });
    if (!busy) {
      return;
    }
    const subscription = BackHandler.addEventListener(
      'hardwareBackPress',
      () => true,
    );
    return () => subscription.remove();
  }, [navigation, busy]);
  const review = async () => {
    if (flight.current) {
      return;
    }
    flight.current = true;
    setPreparing(true);
    setError('');
    const started = currentRevision.current;
    try {
      const value = await build();
      if (mounted.current && started === currentRevision.current) {
        setPrepared(value);
      }
    } catch (failure) {
      if (mounted.current) {
        setError(describeError(failure));
      }
    } finally {
      flight.current = false;
      if (mounted.current) {
        setPreparing(false);
      }
    }
  };
  const send = async () => {
    if (!prepared || flight.current) {
      return;
    }
    flight.current = true;
    try {
      await mutation.submit(service, title, route, prepared.body);
      if (mounted.current) {
        setPrepared(null);
        setSubmitted(true);
        await onSubmitted?.();
      }
    } catch {
      /* The safe error and exact retry are shown by RequestFeedback. */
    } finally {
      flight.current = false;
    }
  };
  const retry = async () => {
    const belongsHere =
      mutation.pending?.route === route && mutation.pending.service === service;
    await mutation.retry();
    if (belongsHere && mounted.current) {
      setPrepared(null);
      setSubmitted(true);
      await onSubmitted?.();
    }
  };
  return (
    <Screen>
      <Text style={layout.title}>{title}</Text>
      <Text style={layout.body}>{description}</Text>
      <RequestFeedback
        error={mutation.error || error}
        receipt={mutation.receipt}
        pending={mutation.pending}
        busy={busy}
        onRetry={retry}
      />
      {!submitted ? (
        <>
          {children(busy)}
          {prepared ? (
            <View style={layout.card}>
              <Text style={layout.heading}>Review your request</Text>
              {prepared.summary.map((line, index) => (
                <Text key={index} style={layout.body}>
                  {line}
                </Text>
              ))}
              <Notice
                tone="info"
                message="This submits your request for company review. It does not approve leave, release money or change your employment."
              />
              <Button
                title="Submit request"
                busy={mutation.busy}
                disabled={busy || !!mutation.pending}
                onPress={() => send()}
              />
              <Button
                title="Edit details"
                secondary
                disabled={busy}
                onPress={() => setPrepared(null)}
              />
            </View>
          ) : (
            <Button
              title={preparing ? 'Preparing review…' : 'Review request'}
              busy={preparing}
              disabled={busy || !ready || !!mutation.pending}
              onPress={() => review()}
            />
          )}
        </>
      ) : null}
      <Button
        title={submitted ? 'Back to requests' : 'Back'}
        secondary
        disabled={busy}
        onPress={() => navigation.goBack()}
      />
    </Screen>
  );
}
