import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useForm, type Resolver } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import {
  Button,
  Credenza,
  CredenzaContent,
  CredenzaDescription,
  CredenzaFooter,
  CredenzaHeader,
  CredenzaTitle,
  CredenzaBody,
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
  FormErrorBanner,
  Input,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
  Textarea,
} from '@uikit'

import { useCreateLead, useContactsQuery, useCompaniesQuery } from '@/portal-app/crm/queries'
import { getRequiredFields, handleFormError } from '@/lib/form'
import { toast } from 'sonner'
import { Loader2, TrendingUp } from 'lucide-react'

const createLeadSchema = (t: (key: string, options?: Record<string, unknown>) => string) =>
  z.object({
    title: z
      .string()
      .min(1, t('validation.required'))
      .max(200, t('validation.maxLength', { count: 200 })),
    contactId: z.string().min(1, t('validation.required')),
    companyId: z.string().optional(),
    value: z.coerce
      .number({ error: t('validation.invalidNumber', { defaultValue: 'Must be a number' }) })
      .min(0, t('validation.minValue', { count: 0 })),
    currency: z.string().optional(),
    expectedCloseDate: z.string().optional(),
    notes: z
      .string()
      .max(1000, t('validation.maxLength', { count: 1000 }))
      .optional(),
  })

type LeadFormData = z.infer<ReturnType<typeof createLeadSchema>>

interface CreateLeadDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  pipelineId: string
}

export const CreateLeadDialog = ({ open, onOpenChange, pipelineId }: CreateLeadDialogProps) => {
  const { t } = useTranslation('common')
  const createLead = useCreateLead()
  const [serverErrors, setServerErrors] = useState<string[]>([])

  const { data: contactsData } = useContactsQuery({ page: 1, pageSize: 100 })
  const { data: companiesData } = useCompaniesQuery({ page: 1, pageSize: 100 })

  const schema = useMemo(() => createLeadSchema(t), [t])
  const requiredFields = useMemo(() => getRequiredFields(schema), [schema])

  const form = useForm<LeadFormData>({
    resolver: zodResolver(schema) as unknown as Resolver<LeadFormData>,
    mode: 'onBlur',
    reValidateMode: 'onChange',
    defaultValues: {
      title: '',
      contactId: '',
      companyId: '',
      value: 0,
      currency: 'USD',
      expectedCloseDate: '',
      notes: '',
    },
  })

  useEffect(() => {
    if (open) {
      setServerErrors([])
      form.reset({
        title: '',
        contactId: '',
        companyId: '',
        value: 0,
        currency: 'USD',
        expectedCloseDate: '',
        notes: '',
      })
    }
  }, [open, form])

  const onSubmit = async (data: LeadFormData) => {
    try {
      await createLead.mutateAsync({
        title: data.title,
        contactId: data.contactId,
        companyId: data.companyId || undefined,
        value: data.value,
        currency: data.currency || undefined,
        pipelineId,
        expectedCloseDate: data.expectedCloseDate || undefined,
        notes: data.notes || undefined,
      })
      toast.success(t('crm.leads.createSuccess', { defaultValue: 'Lead created successfully' }))
      onOpenChange(false)
    } catch (err) {
      handleFormError(err, form, setServerErrors, t)
    }
  }

  const contacts = contactsData?.items ?? []
  const companies = companiesData?.items ?? []
  const isSubmitting = createLead.isPending

  return (
    <Credenza open={open} onOpenChange={onOpenChange}>
      <CredenzaContent className="sm:max-w-[550px]">
        <CredenzaHeader>
          <div className="flex items-center gap-3">
            <div className="p-2 bg-primary/10 rounded-lg">
              <TrendingUp className="h-5 w-5 text-primary" />
            </div>
            <div>
              <CredenzaTitle>
                {t('crm.leads.createLead', { defaultValue: 'Create Lead' })}
              </CredenzaTitle>
              <CredenzaDescription>
                {t('crm.leads.createLeadDescription', { defaultValue: 'Fill in the details to create a new lead.' })}
              </CredenzaDescription>
            </div>
          </div>
        </CredenzaHeader>

        <Form {...form} requiredFields={requiredFields}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <CredenzaBody className="space-y-4">
              <FormErrorBanner
                errors={serverErrors}
                onDismiss={() => setServerErrors([])}
                title={t('validation.unableToSave', { defaultValue: 'Unable to save' })}
              />

              <FormField
                control={form.control}
                name="title"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('crm.leads.title', { defaultValue: 'Deal Title' })}</FormLabel>
                    <FormControl>
                      <Input
                        {...field}
                        placeholder={t('crm.leads.titlePlaceholder', { defaultValue: 'Enter deal title' })}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <div className="grid grid-cols-2 gap-4">
                <FormField
                  control={form.control}
                  name="contactId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('crm.leads.contact', { defaultValue: 'Contact' })}</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value}>
                        <FormControl>
                          <SelectTrigger className="cursor-pointer">
                            <SelectValue placeholder={t('crm.leads.selectContact', { defaultValue: 'Select contact' })} />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {contacts.map((contact) => (
                            <SelectItem key={contact.id} value={contact.id} className="cursor-pointer">
                              {contact.firstName} {contact.lastName}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="companyId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('crm.leads.company', { defaultValue: 'Company' })}</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value ?? ''}>
                        <FormControl>
                          <SelectTrigger className="cursor-pointer">
                            <SelectValue placeholder={t('crm.leads.selectCompany', { defaultValue: 'Select company' })} />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          {companies.map((company) => (
                            <SelectItem key={company.id} value={company.id} className="cursor-pointer">
                              {company.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <FormField
                  control={form.control}
                  name="value"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('crm.leads.value', { defaultValue: 'Value' })}</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          {...field}
                          onChange={(e) => field.onChange(parseFloat(e.target.value) || 0)}
                          min={0}
                          placeholder="0"
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="currency"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('crm.leads.currency', { defaultValue: 'Currency' })}</FormLabel>
                      <Select onValueChange={field.onChange} value={field.value ?? 'USD'}>
                        <FormControl>
                          <SelectTrigger className="cursor-pointer">
                            <SelectValue placeholder="USD" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          <SelectItem value="USD" className="cursor-pointer">USD</SelectItem>
                          <SelectItem value="EUR" className="cursor-pointer">EUR</SelectItem>
                          <SelectItem value="GBP" className="cursor-pointer">GBP</SelectItem>
                          <SelectItem value="VND" className="cursor-pointer">VND</SelectItem>
                        </SelectContent>
                      </Select>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>

              <FormField
                control={form.control}
                name="expectedCloseDate"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('crm.leads.expectedCloseDate', { defaultValue: 'Expected Close Date' })}</FormLabel>
                    <FormControl>
                      <Input
                        type="date"
                        {...field}
                        value={field.value ?? ''}
                        className="cursor-pointer"
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="notes"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('crm.leads.notes', { defaultValue: 'Notes' })}</FormLabel>
                    <FormControl>
                      <Textarea
                        {...field}
                        value={field.value ?? ''}
                        placeholder={t('crm.leads.notesPlaceholder', { defaultValue: 'Add any notes about this lead...' })}
                        rows={3}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </CredenzaBody>

            <CredenzaFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
                className="cursor-pointer"
              >
                {t('labels.cancel', { defaultValue: 'Cancel' })}
              </Button>
              <Button type="submit" disabled={isSubmitting} className="cursor-pointer">
                {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {t('labels.create', { defaultValue: 'Create' })}
              </Button>
            </CredenzaFooter>
          </form>
        </Form>
      </CredenzaContent>
    </Credenza>
  )
}
