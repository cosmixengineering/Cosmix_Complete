import { useTranslation } from 'react-i18next'
import { Award } from 'lucide-react'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  EmptyState,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
  FilePreviewTrigger,
} from '@uikit'
import { formatCurrency } from '@/lib/utils/currency'
import type { TopSellingProduct } from '@/services/dashboard'

interface ProductPerformanceCardProps {
  products: TopSellingProduct[]
}

export const ProductPerformanceCard = ({ products }: ProductPerformanceCardProps) => {
  const { t } = useTranslation('common')

  return (
    <Card className="gap-0 shadow-sm hover:shadow-lg transition-all duration-300 md:col-span-2">
      <CardHeader className="pb-3">
        <CardTitle className="text-lg flex items-center gap-2">
          <Award className="h-5 w-5 text-primary" />
          {t('dashboard.productPerformance')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {products.length === 0 ? (
          <EmptyState
            icon={Award}
            title={t('dashboard.noTopProducts')}
            size="sm"
            className="border-0 rounded-none py-6"
          />
        ) : (
          <div className="rounded-xl border border-border/50 overflow-hidden">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('labels.product', 'Product')}</TableHead>
                  <TableHead className="text-right">{t('dashboard.quantitySold')}</TableHead>
                  <TableHead className="text-right">{t('dashboard.revenue')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {products.slice(0, 5).map((product) => (
                  <TableRow key={product.productId} className="transition-colors hover:bg-muted/50">
                    <TableCell>
                      <div className="flex items-center gap-3">
                        <FilePreviewTrigger
                          file={{
                            url: product.imageUrl ?? '',
                            name: product.productName,
                          }}
                          thumbnailWidth={32}
                          thumbnailHeight={32}
                          className="rounded-lg"
                        />
                        <span className="font-medium text-sm">{product.productName}</span>
                      </div>
                    </TableCell>
                    <TableCell className="text-right font-medium">
                      {product.totalQuantitySold.toLocaleString()}
                    </TableCell>
                    <TableCell className="text-right font-medium">
                      {formatCurrency(product.totalRevenue)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
